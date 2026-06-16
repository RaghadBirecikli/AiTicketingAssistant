import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NotificationItemComponent } from './notification-item.component';
import { NotificationResponse } from '../../models/notification.models';

const notification: NotificationResponse = {
  id: 'notification-id',
  userId: 'recipient-user-id',
  title: 'Ticket assigned',
  message: '<strong>Ticket assigned</strong>',
  type: 'TicketAssigned',
  ticketId: '11111111-1111-4111-8111-111111111111',
  isRead: false,
  createdAtUtc: '2026-06-04T12:00:00+00:00',
  readAtUtc: null
};

describe('NotificationItemComponent', () => {
  let fixture: ComponentFixture<NotificationItemComponent>;
  let component: NotificationItemComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationItemComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationItemComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('notification', notification);
    fixture.detectChanges();
  });

  it('renders title and message as plain text', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('<strong>Ticket assigned</strong>');
    expect(element.querySelector('strong')).toBeNull();
    expect(element.textContent).not.toContain('recipient-user-id');
  });

  it('emits mark-read and open-ticket actions', () => {
    spyOn(component.markRead, 'emit');
    spyOn(component.openTicket, 'emit');

    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    buttons.find(button => button.textContent?.includes('Open related ticket'))?.click();
    buttons.find(button => button.textContent?.includes('Mark as read'))?.click();

    expect(component.openTicket.emit).toHaveBeenCalledWith(notification);
    expect(component.markRead.emit).toHaveBeenCalledWith(notification);
  });

  it('opens the related ticket from the clickable row by mouse and keyboard', () => {
    spyOn(component.openTicket, 'emit');

    const item = (fixture.nativeElement as HTMLElement).querySelector('.notification-item') as HTMLElement;
    expect(item.classList).toContain('clickable');
    expect(item.getAttribute('role')).toBe('button');
    expect(item.getAttribute('tabindex')).toBe('0');

    item.click();
    item.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    item.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));

    expect(component.openTicket.emit).toHaveBeenCalledTimes(3);
    expect(component.openTicket.emit).toHaveBeenCalledWith(notification);
  });

  it('does not render unread action for read notifications or ticket action without ticket id', () => {
    fixture.componentRef.setInput('notification', {
      ...notification,
      isRead: true,
      ticketId: null,
      readAtUtc: '2026-06-04T12:10:00+00:00'
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Read');
    expect(text).not.toContain('Mark as read');
    expect(text).not.toContain('Open related ticket');
    expect((fixture.nativeElement as HTMLElement).querySelector('.notification-item')?.getAttribute('role')).toBeNull();
  });
});
