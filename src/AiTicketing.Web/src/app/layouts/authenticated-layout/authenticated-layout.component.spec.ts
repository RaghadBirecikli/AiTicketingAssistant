import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Component, computed, signal } from '@angular/core';
import { AuthenticatedLayoutComponent } from './authenticated-layout.component';
import { AuthService } from '../../core/auth/auth.service';
import { CurrentUser } from '../../core/auth/auth.models';
import { UserRole } from '../../core/models/role.model';
import { NotificationStateService } from '../../features/notifications/services/notification-state.service';

class AuthServiceStub {
  currentUser = signal<CurrentUser | null>({
    id: 'admin-id',
    email: 'admin@example.com',
    displayName: 'Admin User',
    roles: ['Admin']
  });
  currentRole = signal<UserRole | null>('Admin');
  logout = jasmine.createSpy('logout');
}

class NotificationStateStub {
  unreadCount = signal(3);
  panelOpen = signal(false);
  badgeText = computed(() => String(this.unreadCount()));
  accessibleLabel = computed(() => `Notifications, ${this.unreadCount()} unread`);
  clear = jasmine.createSpy('clear');
  togglePanel = jasmine.createSpy('togglePanel');
  closePanel = jasmine.createSpy('closePanel');
}

@Component({
  standalone: true,
  template: '<p>Next route</p>'
})
class DummyRouteComponent {}

describe('AuthenticatedLayoutComponent', () => {
  let fixture: ComponentFixture<AuthenticatedLayoutComponent>;
  let auth: AuthServiceStub;
  let notifications: NotificationStateStub;
  let mediaListener: ((event: MediaQueryListEvent) => void) | null;

  beforeEach(async () => {
    localStorage.clear();
    mediaListener = null;
    spyOn(window, 'matchMedia').and.returnValue({
      matches: false,
      media: '(prefers-color-scheme: dark)',
      onchange: null,
      addEventListener: jasmine.createSpy('addEventListener').and.callFake((_event: string, listener: (event: MediaQueryListEvent) => void) => {
        mediaListener = listener;
      }),
      removeEventListener: jasmine.createSpy('removeEventListener'),
      addListener: jasmine.createSpy('addListener'),
      removeListener: jasmine.createSpy('removeListener'),
      dispatchEvent: jasmine.createSpy('dispatchEvent')
    } as unknown as MediaQueryList);
    auth = new AuthServiceStub();
    notifications = new NotificationStateStub();
    await TestBed.configureTestingModule({
      imports: [AuthenticatedLayoutComponent],
      providers: [
        provideRouter([{ path: 'next', component: DummyRouteComponent }]),
        { provide: AuthService, useValue: auth },
        { provide: NotificationStateService, useValue: notifications }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AuthenticatedLayoutComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
    document.body.classList.remove('nav-drawer-open');
  });

  it('displays the current user and role without rendering tokens', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Admin User');
    expect(text).toContain('Admin');
    expect(text).toContain('Dashboard');
    expect(text).toContain('Notifications');
    expect(text).not.toContain('Bearer');
    expect(text).not.toContain('jwt');
  });

  it('clears the session on logout', () => {
    const userButton = (fixture.nativeElement as HTMLElement).querySelector('.user-menu-button') as HTMLButtonElement;
    userButton.click();
    fixture.detectChanges();

    const logoutButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.trim() === 'Logout');
    logoutButton?.click();

    expect(auth.logout).toHaveBeenCalled();
    expect(notifications.clear).toHaveBeenCalled();
  });

  it('opens and closes mobile navigation accessibly', fakeAsync(() => {
    const menuButton = (fixture.nativeElement as HTMLElement).querySelector('.mobile-menu') as HTMLButtonElement;
    const shell = (fixture.nativeElement as HTMLElement).querySelector('.shell') as HTMLElement;
    const sidebar = (fixture.nativeElement as HTMLElement).querySelector('.sidebar') as HTMLElement;

    menuButton.click();
    fixture.detectChanges();
    flushMicrotasks();

    expect(menuButton.getAttribute('aria-expanded')).toBe('true');
    expect(shell.classList).toContain('mobile-nav-open');
    expect(document.body.classList).toContain('nav-drawer-open');
    expect(sidebar.getAttribute('aria-label')).toBe('Primary navigation');
    expect(document.activeElement).toBe(sidebar);
    expect((fixture.nativeElement as HTMLElement).querySelector('.mobile-scrim')).not.toBeNull();

    ((fixture.nativeElement as HTMLElement).querySelector('.mobile-scrim') as HTMLButtonElement).click();
    fixture.detectChanges();
    flushMicrotasks();

    expect(menuButton.getAttribute('aria-expanded')).toBe('false');
    expect(shell.classList).not.toContain('mobile-nav-open');
    expect(document.body.classList).not.toContain('nav-drawer-open');
    expect(document.activeElement).toBe(menuButton);
  }));

  it('closes mobile navigation on Escape, route changes, and cleanup', fakeAsync(() => {
    const menuButton = (fixture.nativeElement as HTMLElement).querySelector('.mobile-menu') as HTMLButtonElement;

    menuButton.click();
    fixture.detectChanges();
    flushMicrotasks();
    expect(menuButton.getAttribute('aria-expanded')).toBe('true');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    flushMicrotasks();
    expect(menuButton.getAttribute('aria-expanded')).toBe('false');
    expect(document.body.classList).not.toContain('nav-drawer-open');

    menuButton.click();
    fixture.detectChanges();
    flushMicrotasks();
    void TestBed.inject(Router).navigateByUrl('/next');
    tick();
    fixture.detectChanges();
    flushMicrotasks();
    expect(menuButton.getAttribute('aria-expanded')).toBe('false');
    expect(document.body.classList).not.toContain('nav-drawer-open');

    menuButton.click();
    fixture.detectChanges();
    flushMicrotasks();
    fixture.destroy();

    expect(document.body.classList).not.toContain('nav-drawer-open');
  }));

  it('keeps drawer direction state independent of document direction', fakeAsync(() => {
    const menuButton = (fixture.nativeElement as HTMLElement).querySelector('.mobile-menu') as HTMLButtonElement;
    const shell = (fixture.nativeElement as HTMLElement).querySelector('.shell') as HTMLElement;

    document.documentElement.dir = 'ltr';
    menuButton.click();
    fixture.detectChanges();
    flushMicrotasks();
    expect(shell.classList).toContain('mobile-nav-open');
    expect((fixture.nativeElement as HTMLElement).querySelector('.mobile-scrim')).not.toBeNull();
    ((fixture.nativeElement as HTMLElement).querySelector('.mobile-scrim') as HTMLButtonElement).click();
    fixture.detectChanges();
    flushMicrotasks();

    document.documentElement.dir = 'rtl';
    menuButton.click();
    fixture.detectChanges();
    flushMicrotasks();
    expect(shell.classList).toContain('mobile-nav-open');
    expect((fixture.nativeElement as HTMLElement).querySelector('.mobile-scrim')).not.toBeNull();
  }));

  it('switches and persists theme preference from the shell', () => {
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('.theme-trigger') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    const darkButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.theme-menu [role="menuitemradio"]'))
      .find(button => button.textContent?.trim() === 'Dark');
    darkButton?.click();
    fixture.detectChanges();

    expect(localStorage.getItem('ai-ticketing-theme')).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('opens and closes the theme menu by keyboard', () => {
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('.theme-trigger') as HTMLButtonElement;

    trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect((fixture.nativeElement as HTMLElement).querySelector('.theme-menu')).not.toBeNull();

    trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect((fixture.nativeElement as HTMLElement).querySelector('.theme-menu')).toBeNull();
  });

  it('announces the selected theme preference in the menu', () => {
    fixture.componentInstance.setTheme('light');
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('.theme-trigger') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    const items = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.theme-menu [role="menuitemradio"]'));
    const lightItem = items.find(button => button.textContent?.includes('Light'));
    const systemItem = items.find(button => button.textContent?.includes('Use system setting'));

    expect(lightItem?.getAttribute('aria-checked')).toBe('true');
    expect(systemItem?.getAttribute('aria-checked')).toBe('false');
  });

  it('keeps Light and System visually light while storing distinct preferences', () => {
    fixture.componentInstance.setTheme('light');
    fixture.detectChanges();
    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('light');

    fixture.componentInstance.setTheme('system');
    fixture.detectChanges();

    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('system');
  });

  it('updates System theme when the OS preference changes', () => {
    fixture.componentInstance.setTheme('system');
    fixture.detectChanges();

    mediaListener?.({ matches: true } as MediaQueryListEvent);
    fixture.detectChanges();

    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('keeps the header uncluttered with one compact theme trigger', () => {
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelector('.theme-switcher')).toBeNull();
    expect(host.querySelectorAll('.theme-trigger').length).toBe(1);
    expect(host.querySelector('.theme-trigger')?.textContent?.trim()).toBe('');
  });

  it('links Customer create-ticket navigation to the real creation route', () => {
    auth.currentUser.set({
      id: 'customer-id',
      email: 'customer@example.com',
      displayName: 'Customer User',
      roles: ['Customer']
    });
    auth.currentRole.set('Customer');
    fixture.detectChanges();

    const createLink = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .find(link => link.querySelector('.nav-label')?.textContent?.trim() === 'Create Ticket');

    expect(createLink?.getAttribute('href')).toBe('/customer/tickets/new');
  });

  it('does not expose create-ticket navigation to staff roles', () => {
    auth.currentRole.set('Agent');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Create Ticket');

    auth.currentRole.set('Admin');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Create Ticket');
  });
});
