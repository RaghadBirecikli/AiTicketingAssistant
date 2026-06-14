import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { computed, signal } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { NotificationStateService } from './notification-state.service';
import { NotificationApiService } from '../data-access/notification-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { CurrentUser } from '../../../core/auth/auth.models';
import { UserRole } from '../../../core/models/role.model';
import { NotificationResponse } from '../models/notification.models';

const unreadNotification: NotificationResponse = {
  id: 'notification-1',
  userId: 'user-id',
  title: 'Ticket assigned',
  message: '<strong>Plain text only</strong>',
  type: 'TicketAssigned',
  ticketId: '11111111-1111-4111-8111-111111111111',
  isRead: false,
  createdAtUtc: '2026-06-04T12:00:00+00:00',
  readAtUtc: null
};

const readNotification: NotificationResponse = {
  ...unreadNotification,
  id: 'notification-2',
  isRead: true,
  readAtUtc: '2026-06-04T12:10:00+00:00'
};

const newerRealtimeNotification: NotificationResponse = {
  ...unreadNotification,
  id: 'notification-3',
  title: 'New message',
  createdAtUtc: '2026-06-04T12:30:00+00:00',
  readAtUtc: null
};

class AuthServiceStub {
  currentUser = signal<CurrentUser | null>({
    id: 'user-id',
    email: 'user@example.com',
    displayName: 'User',
    roles: ['Admin']
  });
  currentRole = signal<UserRole | null>('Admin');
  isAuthenticated = computed(() => this.currentUser() !== null);
}

describe('NotificationStateService', () => {
  let api: jasmine.SpyObj<NotificationApiService>;
  let auth: AuthServiceStub;
  let router: Router;
  let service: NotificationStateService;

  beforeEach(() => {
    api = jasmine.createSpyObj<NotificationApiService>('NotificationApiService', [
      'getUnreadCount',
      'getNotifications',
      'markAsRead',
      'markAllAsRead'
    ]);
    api.getUnreadCount.and.returnValue(of({ unreadCount: 3 }));
    api.getNotifications.and.returnValue(of([unreadNotification, readNotification]));
    api.markAsRead.and.returnValue(of({
      notification: {
        ...unreadNotification,
        isRead: true,
        readAtUtc: '2026-06-04T12:11:00+00:00'
      }
    }));
    api.markAllAsRead.and.returnValue(of({ updatedCount: 1 }));
    auth = new AuthServiceStub();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        NotificationStateService,
        { provide: NotificationApiService, useValue: api },
        { provide: AuthService, useValue: auth }
      ]
    });

    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    service = TestBed.inject(NotificationStateService);
    TestBed.flushEffects();
  });

  it('initial authenticated state requests unread count', () => {
    expect(api.getUnreadCount).toHaveBeenCalledTimes(1);
    expect(service.unreadCount()).toBe(3);
  });

  it('loads full list only when panel opens and avoids duplicate loaded requests', () => {
    service.openPanel();
    service.openPanel();

    expect(api.getNotifications).toHaveBeenCalledTimes(1);
    expect(service.notifications().length).toBe(2);
  });

  it('supports retry after list error', () => {
    api.getNotifications.and.returnValues(
      throwError(() => new Error('stack trace')),
      of([unreadNotification])
    );

    service.openPanel();
    expect(service.listError()).toBe('Notifications could not be loaded. Please try again.');

    service.loadNotifications(true);
    expect(api.getNotifications).toHaveBeenCalledTimes(2);
    expect(service.notifications().length).toBe(1);
  });

  it('successful mark-one updates item and decrements unread count safely', () => {
    service.openPanel();
    service.markAsRead(unreadNotification);

    expect(service.notifications().find(notification => notification.id === unreadNotification.id)?.isRead).toBeTrue();
    expect(service.unreadCount()).toBe(0);

    service.markAsRead({ ...unreadNotification, isRead: false });
    expect(service.unreadCount()).toBe(0);
  });

  it('valid incoming unread notification is added newest-first and increments unread count once', () => {
    service.unreadCount.set(3);
    service.receiveRealtimeNotification(newerRealtimeNotification);
    service.receiveRealtimeNotification(newerRealtimeNotification);

    expect(service.notifications()[0].id).toBe('notification-3');
    expect(service.notifications().filter(notification => notification.id === 'notification-3').length).toBe(1);
    expect(service.unreadCount()).toBe(4);
  });

  it('incoming read notification does not increment unread count and malformed payload is ignored', () => {
    service.unreadCount.set(3);

    service.receiveRealtimeNotification({ ...newerRealtimeNotification, isRead: true, id: 'notification-4' });
    service.receiveRealtimeNotification({ id: 'bad-payload' });

    expect(service.notifications().some(notification => notification.id === 'notification-4')).toBeTrue();
    expect(service.unreadCount()).toBe(3);
    expect(service.notifications().some(notification => notification.id === 'bad-payload')).toBeFalse();
  });

  it('REST list merge preserves realtime notifications and avoids duplicates', () => {
    service.receiveRealtimeNotification(newerRealtimeNotification);
    service.mergeNotifications([unreadNotification, readNotification]);

    expect(service.notifications().map(notification => notification.id)).toEqual([
      'notification-3',
      'notification-1',
      'notification-2'
    ]);
  });

  it('duplicate mark-one requests are prevented', () => {
    const pending = new Subject<{ notification: NotificationResponse }>();
    api.markAsRead.and.returnValue(pending.asObservable());
    service.openPanel();

    service.markAsRead(unreadNotification);
    service.markAsRead(unreadNotification);

    expect(api.markAsRead).toHaveBeenCalledTimes(1);

    pending.next({ notification: { ...unreadNotification, isRead: true, readAtUtc: '2026-06-04T12:12:00+00:00' } });
    pending.complete();
  });

  it('mark-one error renders safely', () => {
    api.markAsRead.and.returnValue(throwError(() => new Error('raw failure')));
    service.openPanel();

    service.markAsRead(unreadNotification);

    expect(service.itemError()).toBe('The notification could not be marked as read.');
  });

  it('successful mark-all updates loaded items and sets unread count to zero', () => {
    service.openPanel();
    service.unreadCount.set(2);

    service.markAllAsRead();

    expect(service.unreadCount()).toBe(0);
    expect(service.notifications().every(notification => notification.isRead)).toBeTrue();
    expect(service.statusMessage()).toBe('1 notifications marked as read.');
  });

  it('new live notification after mark-all increments unread count correctly', () => {
    service.openPanel();
    service.unreadCount.set(2);
    service.markAllAsRead();

    service.receiveRealtimeNotification(newerRealtimeNotification);

    expect(service.unreadCount()).toBe(1);
    expect(service.notifications()[0].id).toBe('notification-3');
    expect(service.notifications()[0].isRead).toBeFalse();
  });

  it('duplicate mark-all requests are prevented', () => {
    const pending = new Subject<{ updatedCount: number }>();
    api.markAllAsRead.and.returnValue(pending.asObservable());
    service.unreadCount.set(2);

    service.markAllAsRead();
    service.markAllAsRead();

    expect(api.markAllAsRead).toHaveBeenCalledTimes(1);

    pending.next({ updatedCount: 2 });
    pending.complete();
  });

  it('mark-all error renders safely', () => {
    api.markAllAsRead.and.returnValue(throwError(() => new Error('raw failure')));
    service.unreadCount.set(1);

    service.markAllAsRead();

    expect(service.markAllError()).toBe('Notifications could not be updated. Please try again.');
  });

  it('ticket navigation uses deterministic role routes and Admin precedence', () => {
    service.openTicket(unreadNotification);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/tickets/11111111-1111-4111-8111-111111111111');

    auth.currentRole.set('Agent');
    service.openTicket(unreadNotification);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/agent/tickets/11111111-1111-4111-8111-111111111111');

    auth.currentRole.set('Customer');
    service.openTicket(unreadNotification);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/customer/tickets/11111111-1111-4111-8111-111111111111');
  });

  it('notification without ticket id does not navigate', () => {
    service.openTicket({ ...unreadNotification, ticketId: null });

    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('logout clears notification state and new login reloads unread count', () => {
    service.openPanel();
    expect(service.notifications().length).toBe(2);

    auth.currentUser.set(null);
    TestBed.flushEffects();

    expect(service.notifications()).toEqual([]);
    expect(service.unreadCount()).toBe(0);

    auth.currentUser.set({
      id: 'user-2',
      email: 'user2@example.com',
      displayName: 'User 2',
      roles: ['Customer']
    });
    TestBed.flushEffects();

    expect(api.getUnreadCount).toHaveBeenCalledTimes(2);
  });
});
