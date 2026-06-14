import { ComponentFixture, TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { NotificationBellComponent } from './notification-bell.component';
import { NotificationStateService } from '../../services/notification-state.service';

class NotificationStateStub {
  unreadCount = signal(125);
  panelOpen = signal(false);
  badgeText = computed(() => this.unreadCount() > 99 ? '99+' : String(this.unreadCount()));
  accessibleLabel = computed(() => `Notifications, ${this.unreadCount()} unread`);
  togglePanel = jasmine.createSpy('togglePanel');
  closePanel = jasmine.createSpy('closePanel');
}

describe('NotificationBellComponent', () => {
  let fixture: ComponentFixture<NotificationBellComponent>;
  let notifications: NotificationStateStub;

  beforeEach(async () => {
    notifications = new NotificationStateStub();
    await TestBed.configureTestingModule({
      imports: [NotificationBellComponent],
      providers: [
        { provide: NotificationStateService, useValue: notifications }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationBellComponent);
    fixture.detectChanges();
  });

  it('renders capped visual badge and accessible actual count', () => {
    const button = (fixture.nativeElement as HTMLElement).querySelector('button');

    expect(button?.getAttribute('aria-label')).toBe('Notifications, 125 unread');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('99+');
  });

  it('hides badge when unread count is zero', () => {
    notifications.unreadCount.set(0);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.badge')).toBeNull();
  });

  it('toggles panel and closes on Escape', () => {
    const button = (fixture.nativeElement as HTMLElement).querySelector('button');
    button?.click();
    fixture.detectChanges();

    expect(notifications.togglePanel).toHaveBeenCalled();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(notifications.closePanel).toHaveBeenCalled();
  });
});
