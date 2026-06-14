export const ApiEndpoints = {
  auth: {
    login: '/api/auth/login'
  },
  me: '/api/me',
  tickets: '/api/tickets',
  ticketStats: '/api/tickets/stats',
  myTicketStats: '/api/tickets/my-stats',
  notifications: {
    list: '/api/notifications',
    unreadCount: '/api/notifications/unread-count',
    markAllRead: '/api/notifications/read-all'
  },
  users: {
    agents: '/api/users/agents'
  }
} as const;
