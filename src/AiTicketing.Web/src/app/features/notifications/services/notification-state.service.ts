import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { ApiError } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import { LocalizationService } from '../../../core/localization/localization.service';
import { NotificationApiService } from '../data-access/notification-api.service';
import { NotificationResponse } from '../models/notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationStateService {
  private readonly api = inject(NotificationApiService);
  private readonly authService = inject(AuthService);
  private readonly localization = inject(LocalizationService);
  private readonly router = inject(Router);

  readonly notifications = signal<readonly NotificationResponse[]>([]);
  readonly unreadCount = signal(0);
  readonly isCountLoading = signal(false);
  readonly isListLoading = signal(false);
  readonly isMarkingAll = signal(false);
  readonly listLoaded = signal(false);
  readonly panelOpen = signal(false);
  readonly listError = signal<string | null>(null);
  readonly markAllError = signal<string | null>(null);
  readonly itemError = signal<string | null>(null);
  readonly statusMessage = signal<string | null>(null);
  readonly markingIds = signal<ReadonlySet<string>>(new Set<string>());

  readonly badgeText = computed(() => {
    const count = this.unreadCount();
    return count > 99 ? '99+' : String(count);
  });

  readonly accessibleLabel = computed(() => this.unreadCount() > 0
    ? this.localization.t('notifications.accessible', { count: this.unreadCount() })
    : this.localization.t('notifications.noneAccessible'));

  constructor() {
    effect(() => {
      const user = this.authService.currentUser();
      untracked(() => {
        if (user) {
          this.resetForAuthenticatedUser();
          this.loadUnreadCount();
        } else {
          this.clear();
        }
      });
    });
  }

  togglePanel(): void {
    this.panelOpen() ? this.closePanel() : this.openPanel();
  }

  openPanel(): void {
    this.panelOpen.set(true);
    if (!this.listLoaded()) {
      this.loadNotifications();
    }
  }

  closePanel(): void {
    this.panelOpen.set(false);
  }

  loadUnreadCount(): void {
    if (!this.authService.isAuthenticated() || this.isCountLoading()) {
      return;
    }

    this.isCountLoading.set(true);
    this.api.getUnreadCount()
      .pipe(finalize(() => this.isCountLoading.set(false)))
      .subscribe({
        next: response => this.unreadCount.set(Math.max(0, response.unreadCount)),
        error: () => this.unreadCount.set(0)
      });
  }

  loadNotifications(force = false): void {
    if (!this.authService.isAuthenticated() || this.isListLoading() || (this.listLoaded() && !force)) {
      return;
    }

    this.isListLoading.set(true);
    this.listError.set(null);
    this.itemError.set(null);
    this.statusMessage.set(null);

    this.api.getNotifications()
      .pipe(finalize(() => this.isListLoading.set(false)))
      .subscribe({
        next: notifications => {
          this.mergeNotifications(notifications);
          this.listLoaded.set(true);
          this.syncUnreadCountFromLoadedNotifications();
        },
        error: error => {
          this.listLoaded.set(true);
          this.listError.set(this.safeListError(error));
        }
      });
  }

  markAsRead(notification: NotificationResponse): void {
    if (notification.isRead || this.markingIds().has(notification.id)) {
      return;
    }

    this.itemError.set(null);
    this.statusMessage.set(null);
    this.markingIds.update(ids => new Set([...ids, notification.id]));

    this.api.markAsRead(notification.id)
      .pipe(finalize(() => this.markingIds.update(ids => {
        const next = new Set(ids);
        next.delete(notification.id);
        return next;
      })))
      .subscribe({
        next: response => this.applyReadNotification(response.notification),
        error: () => this.itemError.set(this.localization.t('notifications.itemMarkError'))
      });
  }

  markAllAsRead(): void {
    if (this.isMarkingAll() || this.unreadCount() <= 0) {
      return;
    }

    this.isMarkingAll.set(true);
    const requestedAtUtc = new Date().toISOString();
    this.markAllError.set(null);
    this.itemError.set(null);
    this.statusMessage.set(null);

    this.api.markAllAsRead()
      .pipe(finalize(() => this.isMarkingAll.set(false)))
      .subscribe({
        next: response => {
          const now = new Date().toISOString();
          this.notifications.update(notifications => notifications.map(notification => notification.isRead
            ? notification
            : notification.createdAtUtc <= requestedAtUtc
              ? { ...notification, isRead: true, readAtUtc: notification.readAtUtc ?? now }
              : notification));
          this.unreadCount.set(this.notifications().length > 0
            ? this.countUnread(this.notifications())
            : Math.max(0, this.unreadCount() - response.updatedCount));
          this.statusMessage.set(this.localization.t('notifications.markedCount', { count: response.updatedCount }));
        },
        error: () => this.markAllError.set(this.localization.t('notifications.markError'))
      });
  }

  openTicket(notification: NotificationResponse): void {
    if (!notification.ticketId) {
      return;
    }

    if (!notification.isRead) {
      this.markAsRead(notification);
    }

    void this.router.navigateByUrl(this.ticketRoute(notification.ticketId));
    this.closePanel();
  }

  isMarking(notificationId: string): boolean {
    return this.markingIds().has(notificationId);
  }

  receiveRealtimeNotification(notification: unknown): void {
    if (!this.isNotificationResponse(notification)) {
      return;
    }

    const exists = this.notifications().some(item => item.id === notification.id);
    if (exists) {
      return;
    }

    this.notifications.update(notifications => this.sortNewestFirst([notification, ...notifications]));
    if (!notification.isRead) {
      this.unreadCount.update(count => Math.max(0, count + 1));
    }
  }

  mergeNotifications(notifications: readonly NotificationResponse[]): void {
    this.notifications.update(existing => {
      const byId = new Map<string, NotificationResponse>();
      for (const notification of existing) {
        byId.set(notification.id, notification);
      }

      for (const notification of notifications) {
        const current = byId.get(notification.id);
        byId.set(notification.id, this.mergeNotification(current, notification));
      }

      return this.sortNewestFirst(Array.from(byId.values()));
    });
  }

  clear(): void {
    this.notifications.set([]);
    this.unreadCount.set(0);
    this.isCountLoading.set(false);
    this.isListLoading.set(false);
    this.isMarkingAll.set(false);
    this.listLoaded.set(false);
    this.panelOpen.set(false);
    this.listError.set(null);
    this.markAllError.set(null);
    this.itemError.set(null);
    this.statusMessage.set(null);
    this.markingIds.set(new Set<string>());
  }

  private resetForAuthenticatedUser(): void {
    this.notifications.set([]);
    this.unreadCount.set(0);
    this.listLoaded.set(false);
    this.panelOpen.set(false);
    this.listError.set(null);
    this.markAllError.set(null);
    this.itemError.set(null);
    this.statusMessage.set(null);
    this.markingIds.set(new Set<string>());
  }

  private applyReadNotification(readNotification: NotificationResponse): void {
    const before = this.notifications().find(notification => notification.id === readNotification.id);
    this.notifications.update(notifications => notifications.map(notification =>
      notification.id === readNotification.id ? readNotification : notification));

    if (before && !before.isRead && readNotification.isRead) {
      this.unreadCount.update(count => Math.max(0, count - 1));
    } else {
      this.unreadCount.update(count => Math.max(0, count));
    }
  }

  private mergeNotification(
    current: NotificationResponse | undefined,
    incoming: NotificationResponse
  ): NotificationResponse {
    if (!current) {
      return incoming;
    }

    if (current.isRead && !incoming.isRead) {
      return current;
    }

    return incoming;
  }

  private sortNewestFirst(notifications: readonly NotificationResponse[]): readonly NotificationResponse[] {
    return [...notifications].sort((left, right) =>
      new Date(right.createdAtUtc).getTime() - new Date(left.createdAtUtc).getTime());
  }

  private syncUnreadCountFromLoadedNotifications(): void {
    this.unreadCount.set(this.countUnread(this.notifications()));
  }

  private countUnread(notifications: readonly NotificationResponse[]): number {
    return Math.max(0, notifications.filter(notification => !notification.isRead).length);
  }

  private isNotificationResponse(value: unknown): value is NotificationResponse {
    if (typeof value !== 'object' || value === null) {
      return false;
    }

    const candidate = value as Partial<NotificationResponse>;
    return typeof candidate.id === 'string'
      && typeof candidate.title === 'string'
      && typeof candidate.message === 'string'
      && typeof candidate.type === 'string'
      && typeof candidate.isRead === 'boolean'
      && typeof candidate.createdAtUtc === 'string'
      && (typeof candidate.ticketId === 'string' || candidate.ticketId === null)
      && (typeof candidate.readAtUtc === 'string' || candidate.readAtUtc === null)
      && typeof candidate.userId === 'string';
  }

  private ticketRoute(ticketId: string): string {
    switch (this.authService.currentRole()) {
      case 'Admin':
        return `/admin/tickets/${ticketId}`;
      case 'Agent':
        return `/agent/tickets/${ticketId}`;
      case 'Customer':
        return `/customer/tickets/${ticketId}`;
      default:
        return `/customer/tickets/${ticketId}`;
    }
  }

  private safeListError(error: unknown): string {
    if (error instanceof ApiError && error.kind === 'forbidden') {
      return this.localization.t('notifications.loadError');
    }

    return this.localization.t('notifications.loadError');
  }
}
