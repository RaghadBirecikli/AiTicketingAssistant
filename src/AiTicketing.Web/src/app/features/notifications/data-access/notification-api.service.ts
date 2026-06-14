import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoints } from '../../../core/api/api-endpoints';
import { ApiService } from '../../../core/api/api.service';
import {
  MarkAllNotificationsAsReadResponse,
  MarkNotificationAsReadResponse,
  NotificationResponse,
  UnreadNotificationCountResponse
} from '../models/notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly api = inject(ApiService);

  getNotifications(): Observable<readonly NotificationResponse[]> {
    return this.api.get<readonly NotificationResponse[]>(ApiEndpoints.notifications.list);
  }

  getUnreadCount(): Observable<UnreadNotificationCountResponse> {
    return this.api.get<UnreadNotificationCountResponse>(ApiEndpoints.notifications.unreadCount);
  }

  markAsRead(notificationId: string): Observable<MarkNotificationAsReadResponse> {
    return this.api.patch<Record<string, never>, MarkNotificationAsReadResponse>(
      `${ApiEndpoints.notifications.list}/${encodeURIComponent(notificationId)}/read`,
      {}
    );
  }

  markAllAsRead(): Observable<MarkAllNotificationsAsReadResponse> {
    return this.api.patch<Record<string, never>, MarkAllNotificationsAsReadResponse>(
      ApiEndpoints.notifications.markAllRead,
      {}
    );
  }
}
