import { TestBed } from '@angular/core/testing';
import { TicketListService } from './ticket-list.service';
import { ApiService } from '../../../core/api/api.service';
import { of } from 'rxjs';

describe('TicketListService', () => {
  let api: jasmine.SpyObj<ApiService>;
  let service: TicketListService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get', 'post', 'patch']);
    TestBed.configureTestingModule({
      providers: [
        TicketListService,
        { provide: ApiService, useValue: api }
      ]
    });

    service = TestBed.inject(TicketListService);
  });

  it('builds the correct default ticket query', () => {
    service.getTickets({ page: 1, pageSize: 20 }, 'Admin');

    expect(api.get).toHaveBeenCalledWith('/api/tickets', { page: 1, pageSize: 20 });
  });

  it('sends search, filters, sorting, and pagination as server query parameters', () => {
    service.getTickets({
      page: 2,
      pageSize: 50,
      search: 'payment',
      status: 'Open',
      priority: 'High',
      sortBy: 'priority',
      sortDirection: 'desc'
    }, 'Agent');

    expect(api.get).toHaveBeenCalledWith('/api/tickets', {
      page: 2,
      pageSize: 50,
      search: 'payment',
      status: 'Open',
      priority: 'High',
      sortBy: 'priority',
      sortDirection: 'desc'
    });
  });

  it('omits assigned filters for Agent and Customer', () => {
    service.getTickets({
      page: 1,
      pageSize: 20,
      assignedToUserId: 'agent-id',
      unassigned: true
    }, 'Customer');

    expect(api.get).toHaveBeenCalledWith('/api/tickets', { page: 1, pageSize: 20 });
  });

  it('loads Admin agent options from the actual endpoint', () => {
    service.getAgents();

    expect(api.get).toHaveBeenCalledWith('/api/users/agents');
  });

  it('calls the actual ticket details endpoint by id', () => {
    service.getTicketById('11111111-1111-4111-8111-111111111111');

    expect(api.get).toHaveBeenCalledWith('/api/tickets/11111111-1111-4111-8111-111111111111');
  });

  it('create-ticket service uses the exact backend endpoint and body', () => {
    api.post.and.returnValue(of({
      ticket: {
        id: 'ticket-id',
        title: 'Payment page is broken',
        description: 'Cannot pay',
        status: 'Open',
        priority: 'High',
        category: 'Billing',
        source: 'Web',
        customerEmail: null,
        customerName: null,
        customerUserId: 'customer-id',
        assignedToUserId: null,
        createdAtUtc: '2026-06-04T12:00:00+00:00',
        updatedAtUtc: null,
        resolvedAtUtc: null,
        closedAtUtc: null
      },
      aiSummary: 'Payment issue',
      suggestedReply: 'Thanks for reaching out.'
    }));

    service.createTicket('Payment page is broken', 'Cannot pay').subscribe();

    expect(api.post).toHaveBeenCalledWith('/api/tickets', {
      title: 'Payment page is broken',
      description: 'Cannot pay',
      customerEmail: null,
      customerName: null,
      source: 'Web'
    });
  });

  it('public-message service calls the exact backend endpoint', () => {
    api.post.and.returnValue(of({
      message: {
        id: 'message-id',
        ticketId: 'ticket-id',
        message: 'Hello',
        isInternalNote: false,
        createdByUserId: 'user-id',
        createdByDisplayName: 'User',
        createdAtUtc: '2026-06-04T12:00:00+00:00'
      }
    }));

    service.addMessage('ticket-id', 'Hello').subscribe();

    expect(api.post).toHaveBeenCalledWith('/api/tickets/ticket-id/messages', {
      message: 'Hello',
      isInternalNote: false,
      createdByUserId: null,
      createdByDisplayName: null
    });
  });

  it('internal-note service calls the exact backend endpoint', () => {
    api.post.and.returnValue(of({
      message: {
        id: 'message-id',
        ticketId: 'ticket-id',
        senderUserId: 'user-id',
        senderRole: 'Admin',
        senderDisplayName: 'Admin',
        body: 'Note',
        isInternalNote: true,
        createdAtUtc: '2026-06-04T12:00:00+00:00'
      }
    }));

    service.addInternalNote('ticket-id', 'Note').subscribe();

    expect(api.post).toHaveBeenCalledWith('/api/tickets/ticket-id/internal-notes', { body: 'Note' });
  });

  it('assignment service uses the exact backend endpoint and verb', () => {
    api.patch.and.returnValue(of({
      ticket: {
        id: 'ticket-id',
        title: 'Ticket',
        description: 'Description',
        status: 'InProgress',
        priority: 'High',
        category: 'Billing',
        source: 'Web',
        customerEmail: null,
        customerName: null,
        customerUserId: null,
        assignedToUserId: 'agent-id',
        createdAtUtc: '2026-06-04T12:00:00+00:00',
        updatedAtUtc: null,
        resolvedAtUtc: null,
        closedAtUtc: null
      }
    }));

    service.assignTicket('ticket-id', 'agent-id').subscribe();

    expect(api.patch).toHaveBeenCalledWith('/api/tickets/ticket-id/assign', {
      assignedToUserId: 'agent-id'
    });
  });

  it('status service uses the exact backend endpoint and verb', () => {
    api.patch.and.returnValue(of({
      ticket: {
        id: 'ticket-id',
        title: 'Ticket',
        description: 'Description',
        status: 'Resolved',
        priority: 'High',
        category: 'Billing',
        source: 'Web',
        customerEmail: null,
        customerName: null,
        customerUserId: null,
        assignedToUserId: null,
        createdAtUtc: '2026-06-04T12:00:00+00:00',
        updatedAtUtc: null,
        resolvedAtUtc: null,
        closedAtUtc: null
      }
    }));

    service.changeStatus('ticket-id', 'Resolved').subscribe();

    expect(api.patch).toHaveBeenCalledWith('/api/tickets/ticket-id/status', {
      status: 'Resolved',
      changedByUserId: null,
      changedByDisplayName: null
    });
  });
});
