import { AgentLookup, TicketCategory, TicketPriority, TicketSortField, TicketStatus } from './ticket-list.models';

const statusLabels: Record<TicketStatus, string> = {
  Open: 'Open',
  InProgress: 'In Progress',
  WaitingForCustomer: 'Waiting for Customer',
  Resolved: 'Resolved',
  Closed: 'Closed'
};

const priorityLabels: Record<TicketPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Urgent: 'Urgent'
};

const categoryLabels: Record<TicketCategory, string> = {
  General: 'General',
  Bug: 'Bug',
  FeatureRequest: 'Feature Request',
  Billing: 'Billing',
  TechnicalSupport: 'Technical Support',
  Complaint: 'Complaint'
};

const sortFieldLabels: Record<TicketSortField, string> = {
  createdAt: 'Created date',
  updatedAt: 'Updated date',
  priority: 'Priority',
  status: 'Status'
};

export function ticketStatusLabel(status: TicketStatus | string | null | undefined): string {
  return status && status in statusLabels ? statusLabels[status as TicketStatus] : 'Unknown';
}

export function ticketPriorityLabel(priority: TicketPriority | string | null | undefined): string {
  return priority && priority in priorityLabels ? priorityLabels[priority as TicketPriority] : 'Unknown';
}

export function ticketCategoryLabel(category: TicketCategory | string | null | undefined): string {
  return category && category in categoryLabels ? categoryLabels[category as TicketCategory] : 'Unknown';
}

export function ticketSortFieldLabel(sortField: TicketSortField | string | null | undefined): string {
  return sortField && sortField in sortFieldLabels ? sortFieldLabels[sortField as TicketSortField] : 'Created date';
}

export function agentDisplayName(agent: AgentLookup | null | undefined): string | null {
  return agent?.displayName || agent?.email || null;
}

export function assignedAgentLabel(
  assignedToUserId: string | null | undefined,
  agents: readonly AgentLookup[] = [],
  currentUserId?: string | null,
  currentUserDisplayName?: string | null
): string {
  if (!assignedToUserId) {
    return 'Unassigned';
  }

  const agent = agents.find(item => item.id === assignedToUserId);
  const lookupName = agentDisplayName(agent);
  if (lookupName) {
    return lookupName;
  }

  if (currentUserId && assignedToUserId === currentUserId && currentUserDisplayName) {
    return currentUserDisplayName;
  }

  return 'Assigned agent';
}
