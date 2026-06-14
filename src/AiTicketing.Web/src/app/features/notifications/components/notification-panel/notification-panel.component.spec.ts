import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { NotificationPanelComponent } from './notification-panel.component';
import { NotificationStateService } from '../../services/notification-state.service';
import { NotificationRealtimeService } from '../../services/notification-realtime.service';
import { NotificationResponse } from '../../models/notification.models';

const notification: NotificationResponse = {
  id: 'notification-id',
  userId: 'recipient-user-id',
  title: 'Ticket assigned',
  message: 'Ticket assigned to you.',
  type: 'TicketAssigned',
  ticketId: '11111111-1111-4111-8111-111111111111',
  isRead: false,
  createdAtUtc: '2026-06-04T12:00:00+00:00',
  readAtUtc: null
};

class NotificationStateStub {
  unreadCount = signal(1);
  notifications = signal<readonly NotificationResponse[]>([notification]);
  isListLoading = signal(false);
  isMarkingAll = signal(false);
  listError = signal<string | null>(null);
  markAllError = signal<string | null>(null);
  itemError = signal<string | null>(null);
  statusMessage = signal<string | null>(null);
  loadNotifications = jasmine.createSpy('loadNotifications');
  markAllAsRead = jasmine.createSpy('markAllAsRead');
  markAsRead = jasmine.createSpy('markAsRead');
  openTicket = jasmine.createSpy('openTicket');
  isMarking = jasmine.createSpy('isMarking').and.returnValue(false);
}

class NotificationRealtimeStub {
  connectionState = signal('connected');
}

describe('NotificationPanelComponent', () => {
  let fixture: ComponentFixture<NotificationPanelComponent>;
  let notifications: NotificationStateStub;

  beforeEach(async () => {
    notifications = new NotificationStateStub();
    await TestBed.configureTestingModule({
      imports: [NotificationPanelComponent],
      providers: [
        { provide: NotificationStateService, useValue: notifications },
        { provide: NotificationRealtimeService, useValue: new NotificationRealtimeStub() }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationPanelComponent);
    fixture.detectChanges();
  });

  it('renders notification title, message, date, and unread text', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Ticket assigned');
    expect(text).toContain('Ticket assigned to you.');
    expect(text).toContain('Unread');
    expect(text).not.toContain('recipient-user-id');
  });

  it('renders empty notification state', () => {
    notifications.notifications.set([]);
    notifications.unreadCount.set(0);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No notifications yet.');
  });

  it('renders loading and safe list error states', () => {
    notifications.notifications.set([]);
    notifications.isListLoading.set(true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading notifications...');

    notifications.isListLoading.set(false);
    notifications.listError.set('Notifications could not be loaded. Please try again.');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Notifications could not be loaded. Please try again.');
  });

  it('retry reloads notifications', () => {
    notifications.notifications.set([]);
    notifications.listError.set('Notifications could not be loaded. Please try again.');
    fixture.detectChanges();

    const retryButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.trim() === 'Retry');
    retryButton?.click();

    expect(notifications.loadNotifications).toHaveBeenCalledWith(true);
  });

  it('mark-all and item actions call state service methods', () => {
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));

    buttons.find(button => button.textContent?.includes('Mark all as read'))?.click();
    buttons.find(button => button.textContent?.includes('Mark as read'))?.click();
    buttons.find(button => button.textContent?.includes('Open related ticket'))?.click();

    expect(notifications.markAllAsRead).toHaveBeenCalled();
    expect(notifications.markAsRead).toHaveBeenCalledWith(notification);
    expect(notifications.openTicket).toHaveBeenCalledWith(notification);
  });
});
