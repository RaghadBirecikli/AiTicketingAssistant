export interface AdminTicketStats {
  total: number;
  open: number;
  inProgress: number;
  resolved: number;
  closed: number;
  unassigned: number;
  lowPriority: number;
  mediumPriority: number;
  highPriority: number;
  urgentPriority: number;
}

export interface MyTicketStats {
  total: number;
  open: number;
  inProgress: number;
  resolved: number;
  closed: number;
  lowPriority: number;
  mediumPriority: number;
  highPriority: number;
  urgentPriority: number;
}

export type DashboardStats = AdminTicketStats | MyTicketStats;

export function hasAdminStatsFields(stats: DashboardStats): stats is AdminTicketStats {
  return 'unassigned' in stats;
}
