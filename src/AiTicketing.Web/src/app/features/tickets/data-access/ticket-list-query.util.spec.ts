import { convertToParamMap } from '@angular/router';
import { parseTicketListQuery, ticketListQueryToParams } from './ticket-list-query.util';

describe('ticket list query utilities', () => {
  it('builds the default query', () => {
    const query = parseTicketListQuery(convertToParamMap({}), 'Admin');

    expect(query).toEqual({
      page: 1,
      pageSize: 20
    });
  });

  it('trims search and omits empty search', () => {
    const trimmed = parseTicketListQuery(convertToParamMap({ search: '  payment  ' }), 'Admin');
    const empty = parseTicketListQuery(convertToParamMap({ search: '   ' }), 'Admin');

    expect(trimmed.search).toBe('payment');
    expect(empty.search).toBeUndefined();
  });

  it('uses backend enum values for status and priority case-insensitively', () => {
    const query = parseTicketListQuery(convertToParamMap({ status: 'inprogress', priority: 'urgent' }), 'Agent');

    expect(query.status).toBe('InProgress');
    expect(query.priority).toBe('Urgent');
  });

  it('ignores role-inappropriate assigned filters', () => {
    const agentQuery = parseTicketListQuery(convertToParamMap({ assignedToUserId: 'other-agent', unassigned: 'true' }), 'Agent');
    const customerQuery = parseTicketListQuery(convertToParamMap({ assignedToUserId: 'agent-id' }), 'Customer');

    expect(agentQuery.assignedToUserId).toBeUndefined();
    expect(agentQuery.unassigned).toBeUndefined();
    expect(customerQuery.assignedToUserId).toBeUndefined();
  });

  it('prevents contradictory Admin assigned and unassigned parameters', () => {
    const query = parseTicketListQuery(convertToParamMap({ assignedToUserId: 'agent-id', unassigned: 'true' }), 'Admin');

    expect(query.assignedToUserId).toBeUndefined();
    expect(query.unassigned).toBeUndefined();
  });

  it('serializes Admin unassigned without assignedToUserId', () => {
    const params = ticketListQueryToParams({
      page: 1,
      pageSize: 20,
      assignedToUserId: 'agent-id',
      unassigned: true
    }, 'Admin');

    expect(params['unassigned']).toBeTrue();
    expect(params['assignedToUserId']).toBeUndefined();
  });

  it('serializes Admin assigned agent without unassigned', () => {
    const params = ticketListQueryToParams({
      page: 1,
      pageSize: 20,
      assignedToUserId: 'agent-id'
    }, 'Admin');

    expect(params['assignedToUserId']).toBe('agent-id');
    expect(params['unassigned']).toBeUndefined();
  });

  it('keeps valid route state and ignores invalid values safely', () => {
    const query = parseTicketListQuery(convertToParamMap({
      page: '2',
      pageSize: '50',
      sortBy: ' UPDATEDAT ',
      sortDirection: ' ASC ',
      status: 'bad-status'
    }), 'Admin');

    expect(query.page).toBe(2);
    expect(query.pageSize).toBe(50);
    expect(query.sortBy).toBe('updatedAt');
    expect(query.sortDirection).toBe('asc');
    expect(query.status).toBeUndefined();
  });
});
