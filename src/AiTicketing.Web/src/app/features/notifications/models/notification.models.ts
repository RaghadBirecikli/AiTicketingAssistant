export interface NotificationResponse {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: string;
  ticketId: string | null;
  isRead: boolean;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface UnreadNotificationCountResponse {
  unreadCount: number;
}

export interface MarkNotificationAsReadResponse {
  notification: NotificationResponse;
}

export interface MarkAllNotificationsAsReadResponse {
  updatedCount: number;
}
