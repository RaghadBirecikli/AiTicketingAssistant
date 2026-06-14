import { ParamMap } from '@angular/router';
import { UserRole } from '../../../core/models/role.model';
import {
  pageSizeOptions,
  SortDirection,
  sortDirections,
  TicketListQuery,
  ticketPriorities,
  TicketPriority,
  ticketSortFields,
  TicketSortField,
  ticketStatuses,
  TicketStatus
} from '../models/ticket-list.models';

export const defaultTicketListQuery: TicketListQuery = {
  page: 1,
  pageSize: 20,
  sortBy: 'createdAt',
  sortDirection: 'desc'
};

export function parseTicketListQuery(params: ParamMap, role: UserRole | null): TicketListQuery {
  const query: TicketListQuery = {
    page: positiveInt(params.get('page')) ?? defaultTicketListQuery.page,
    pageSize: pageSize(params.get('pageSize')) ?? defaultTicketListQuery.pageSize
  };

  const search = params.get('search')?.trim();
  if (search) {
    query.search = search;
  }

  const status = enumValue<TicketStatus>(params.get('status'), ticketStatuses);
  if (status) {
    query.status = status;
  }

  const priority = enumValue<TicketPriority>(params.get('priority'), ticketPriorities);
  if (priority) {
    query.priority = priority;
  }

  const sortBy = enumValue<TicketSortField>(params.get('sortBy')?.trim(), ticketSortFields);
  if (sortBy) {
    query.sortBy = sortBy;
  }

  const sortDirection = enumValue<SortDirection>(params.get('sortDirection')?.trim().toLowerCase(), sortDirections);
  if (sortDirection) {
    query.sortDirection = sortDirection;
  } else if (sortBy) {
    query.sortDirection = 'desc';
  }

  if (role === 'Admin') {
    const assignedToUserId = params.get('assignedToUserId')?.trim();
    const unassigned = params.get('unassigned') === 'true';

    if (assignedToUserId && !unassigned) {
      query.assignedToUserId = assignedToUserId;
    } else if (unassigned && !assignedToUserId) {
      query.unassigned = true;
    }
  }

  return query;
}

export function ticketListQueryToParams(query: TicketListQuery, role: UserRole | null): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {
    page: Math.max(1, query.page),
    pageSize: query.pageSize
  };

  if (query.search?.trim()) {
    params['search'] = query.search.trim();
  }

  if (query.status) {
    params['status'] = query.status;
  }

  if (query.priority) {
    params['priority'] = query.priority;
  }

  if (role === 'Admin') {
    if (query.unassigned) {
      params['unassigned'] = true;
    } else if (query.assignedToUserId?.trim()) {
      params['assignedToUserId'] = query.assignedToUserId.trim();
    }
  }

  if (query.sortBy) {
    params['sortBy'] = query.sortBy;
    params['sortDirection'] = query.sortDirection ?? 'desc';
  }

  return params;
}

function enumValue<T extends string>(value: string | null | undefined, allowed: readonly T[]): T | undefined {
  if (!value) {
    return undefined;
  }

  return allowed.find(item => item.toLowerCase() === value.toLowerCase());
}

function positiveInt(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 1 ? parsed : undefined;
}

function pageSize(value: string | null): number | undefined {
  const parsed = positiveInt(value);
  return parsed && pageSizeOptions.includes(parsed as never) ? parsed : undefined;
}
