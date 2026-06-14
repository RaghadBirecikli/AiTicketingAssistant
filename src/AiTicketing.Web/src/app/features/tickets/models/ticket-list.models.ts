export const ticketStatuses = ['Open', 'InProgress', 'WaitingForCustomer', 'Resolved', 'Closed'] as const;
export type TicketStatus = typeof ticketStatuses[number];

export const ticketPriorities = ['Low', 'Medium', 'High', 'Urgent'] as const;
export type TicketPriority = typeof ticketPriorities[number];

export type TicketCategory = 'General' | 'Bug' | 'FeatureRequest' | 'Billing' | 'TechnicalSupport' | 'Complaint';
export type TicketSource = 'Web' | 'Email' | 'Phone' | 'Internal';

export const ticketSortFields = ['createdAt', 'updatedAt', 'priority', 'status'] as const;
export type TicketSortField = typeof ticketSortFields[number];

export const sortDirections = ['asc', 'desc'] as const;
export type SortDirection = typeof sortDirections[number];

export const pageSizeOptions = [10, 20, 50, 100] as const;
export type TicketPageSize = typeof pageSizeOptions[number];

export interface TicketListItem {
  id: string;
  title: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: TicketCategory;
  source: TicketSource;
  customerEmail: string | null;
  customerName: string | null;
  customerUserId: string | null;
  assignedToUserId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
}

export interface PagedTicketList {
  items: readonly TicketListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface TicketListQuery {
  search?: string;
  status?: TicketStatus;
  priority?: TicketPriority;
  assignedToUserId?: string;
  unassigned?: boolean;
  page: number;
  pageSize: number;
  sortBy?: TicketSortField;
  sortDirection?: SortDirection;
}

export interface AgentLookup {
  id: string;
  email: string | null;
  displayName: string | null;
}

export type AssignmentFilterValue = 'all' | 'unassigned' | string;

export interface TicketDetailsMessage {
  id: string;
  ticketId: string;
  senderUserId: string | null;
  senderRole: string;
  senderDisplayName: string | null;
  body: string;
  isInternalNote: boolean;
  createdAtUtc: string;
}

export interface TicketDetails extends TicketListItem {
  messages: readonly TicketDetailsMessage[];
}

export interface CreateTicketRequest {
  title: string;
  description: string;
  customerEmail: string | null;
  customerName: string | null;
  source: TicketSource;
}

export interface CreateTicketResponse {
  ticket: TicketListItem;
  aiSummary: string;
  suggestedReply: string;
}

export interface AddTicketMessageRequest {
  message: string;
  isInternalNote: boolean;
  createdByUserId: string | null;
  createdByDisplayName: string | null;
}

export interface AddTicketMessageResponse {
  message: {
    id: string;
    ticketId: string;
    message: string;
    isInternalNote: boolean;
    createdByUserId: string | null;
    createdByDisplayName: string | null;
    createdAtUtc: string;
  };
}

export interface AddInternalNoteRequest {
  body: string;
}

export interface AddInternalNoteResponse {
  message: TicketDetailsMessage;
}

export interface AssignTicketRequest {
  assignedToUserId: string;
}

export interface AssignTicketResponse {
  ticket: TicketListItem;
  ticketId: string;
  assignedToUserId: string;
  assignedToDisplayName: string | null;
  assignedByUserId: string | null;
  assignedByDisplayName: string | null;
  assignedAtUtc: string;
}

export interface ChangeTicketStatusRequest {
  status: TicketStatus;
  changedByUserId: string | null;
  changedByDisplayName: string | null;
}

export interface ChangeTicketStatusResponse {
  ticket: TicketListItem;
}
