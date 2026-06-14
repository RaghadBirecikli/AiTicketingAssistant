import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { TicketDashboardPageComponent } from './ticket-dashboard-page.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { UserRole } from '../../../../core/models/role.model';
import { DashboardService } from '../../data-access/dashboard.service';
import { AdminTicketStats, MyTicketStats } from '../../models/dashboard-stats.models';
import { ApiError } from '../../../../core/api/api-error';

const adminStats: AdminTicketStats = {
  total: 42,
  open: 10,
  inProgress: 15,
  resolved: 12,
  closed: 5,
  unassigned: 8,
  lowPriority: 4,
  mediumPriority: 20,
  highPriority: 12,
  urgentPriority: 6
};

const myStats: MyTicketStats = {
  total: 12,
  open: 3,
  inProgress: 4,
  resolved: 3,
  closed: 2,
  lowPriority: 1,
  mediumPriority: 5,
  highPriority: 4,
  urgentPriority: 2
};

class AuthServiceStub {
  currentRole = signal<UserRole | null>('Admin');
}

describe('TicketDashboardPageComponent', () => {
  let fixture: ComponentFixture<TicketDashboardPageComponent>;
  let dashboardService: jasmine.SpyObj<DashboardService>;
  let auth: AuthServiceStub;

  beforeEach(async () => {
    auth = new AuthServiceStub();
    dashboardService = jasmine.createSpyObj<DashboardService>('DashboardService', ['getAdminStats', 'getMyStats']);
    dashboardService.getAdminStats.and.returnValue(of(adminStats));
    dashboardService.getMyStats.and.returnValue(of(myStats));

    await TestBed.configureTestingModule({
      imports: [TicketDashboardPageComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: DashboardService, useValue: dashboardService }
      ]
    }).compileComponents();
  });

  function createComponent(): void {
    fixture = TestBed.createComponent(TicketDashboardPageComponent);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function hrefs(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .map(link => link.getAttribute('href') ?? '');
  }

  it('Admin dashboard calls only GET /api/tickets/stats through the dashboard service', () => {
    createComponent();

    expect(dashboardService.getAdminStats).toHaveBeenCalledTimes(1);
    expect(dashboardService.getMyStats).not.toHaveBeenCalled();
  });

  it('Agent dashboard calls only GET /api/tickets/my-stats through the dashboard service', () => {
    auth.currentRole.set('Agent');
    createComponent();

    expect(dashboardService.getMyStats).toHaveBeenCalledTimes(1);
    expect(dashboardService.getAdminStats).not.toHaveBeenCalled();
  });

  it('Customer dashboard calls only GET /api/tickets/my-stats through the dashboard service', () => {
    auth.currentRole.set('Customer');
    createComponent();

    expect(dashboardService.getMyStats).toHaveBeenCalledTimes(1);
    expect(dashboardService.getAdminStats).not.toHaveBeenCalled();
  });

  it('renders actual Admin response values including unassigned and urgent counts', () => {
    createComponent();

    expect(text()).toContain('Total tickets');
    expect(text()).toContain('42');
    expect(text()).toContain('Open');
    expect(text()).toContain('10');
    expect(text()).toContain('Unassigned');
    expect(text()).toContain('8');
    expect(text()).toContain('Urgent');
    expect(text()).toContain('6');
  });

  it('renders actual Agent response values without Admin-only labels', () => {
    auth.currentRole.set('Agent');
    createComponent();

    expect(text()).toContain('Assigned tickets');
    expect(text()).toContain('12');
    expect(text()).toContain('Urgent');
    expect(text()).toContain('2');
    expect(text()).not.toContain('Unassigned');
  });

  it('renders Customer response values with customer-appropriate labels', () => {
    auth.currentRole.set('Customer');
    createComponent();

    expect(text()).toContain('Owned tickets');
    expect(text()).toContain('12');
    expect(text()).not.toContain('Assigned tickets');
    expect(text()).not.toContain('Unassigned');
  });

  it('renders zero values as valid dashboard data', () => {
    dashboardService.getAdminStats.and.returnValue(of({
      total: 0,
      open: 0,
      inProgress: 0,
      resolved: 0,
      closed: 0,
      unassigned: 0,
      lowPriority: 0,
      mediumPriority: 0,
      highPriority: 0,
      urgentPriority: 0
    }));

    createComponent();

    expect(text()).toContain('Total tickets');
    expect(text()).toContain('0');
    expect(text()).not.toContain('could not be loaded');
  });

  it('renders loading state before the first stats response', () => {
    const pending = new Subject<AdminTicketStats>();
    dashboardService.getAdminStats.and.returnValue(pending.asObservable());

    createComponent();

    expect(text()).toContain('Loading dashboard statistics...');

    pending.next(adminStats);
    pending.complete();
  });

  it('renders safe 403 and generic errors without raw messages', () => {
    dashboardService.getAdminStats.and.returnValue(throwError(() => new ApiError(403, 'forbidden', 'database detail')));
    createComponent();

    expect(text()).toContain('You are not allowed to view these dashboard statistics.');
    expect(text()).not.toContain('database detail');

    dashboardService.getAdminStats.and.returnValue(throwError(() => new Error('stack trace')));
    fixture.componentInstance.refresh();
    fixture.detectChanges();

    expect(text()).toContain('Dashboard statistics could not be loaded. Please try again.');
    expect(text()).not.toContain('stack trace');
  });

  it('retry requests dashboard statistics again', () => {
    dashboardService.getAdminStats.and.returnValues(
      throwError(() => new ApiError(503, 'unavailable', 'Down')),
      of(adminStats)
    );
    createComponent();

    const retryButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.trim() === 'Retry');
    retryButton?.click();
    fixture.detectChanges();

    expect(dashboardService.getAdminStats).toHaveBeenCalledTimes(2);
    expect(text()).toContain('42');
  });

  it('prevents duplicate refresh requests while loading', () => {
    const pending = new Subject<AdminTicketStats>();
    dashboardService.getAdminStats.and.returnValue(pending.asObservable());
    createComponent();

    fixture.componentInstance.refresh();

    expect(dashboardService.getAdminStats).toHaveBeenCalledTimes(1);

    pending.next(adminStats);
    pending.complete();
  });

  it('keeps existing stats visible during a background refresh and announces completion', () => {
    const pending = new Subject<AdminTicketStats>();
    dashboardService.getAdminStats.and.returnValues(of(adminStats), pending.asObservable());
    createComponent();

    fixture.componentInstance.refresh();
    fixture.detectChanges();

    expect(text()).toContain('42');
    expect(text()).toContain('Refreshing...');

    pending.next({ ...adminStats, total: 43 });
    pending.complete();
    fixture.detectChanges();

    expect(text()).toContain('Dashboard statistics refreshed.');
    expect(text()).toContain('43');
  });

  it('Admin quick actions use supported ticket-list filters', () => {
    createComponent();

    expect(hrefs()).toContain('/admin/tickets');
    expect(hrefs()).toContain('/admin/tickets?status=Open');
    expect(hrefs()).toContain('/admin/tickets?unassigned=true');
    expect(hrefs()).toContain('/admin/tickets?priority=Urgent');
  });

  it('Agent quick actions route to Agent tickets without Admin-only filters', () => {
    auth.currentRole.set('Agent');
    createComponent();

    expect(hrefs()).toContain('/agent/tickets');
    expect(hrefs()).toContain('/agent/tickets?status=Open');
    expect(hrefs()).toContain('/agent/tickets?priority=Urgent');
    expect(hrefs().some(href => href.includes('unassigned'))).toBeFalse();
  });

  it('Customer quick actions route to Customer tickets and creation without Admin-only filters', () => {
    auth.currentRole.set('Customer');
    createComponent();

    expect(hrefs()).toContain('/customer/tickets');
    expect(hrefs()).toContain('/customer/tickets/new');
    expect(hrefs()).toContain('/customer/tickets?status=Open');
    expect(hrefs().some(href => href.includes('unassigned'))).toBeFalse();
  });

  it('stat cards expose accessible labels', () => {
    createComponent();

    const labels = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('app-dashboard-stat-card article'))
      .map(card => card.getAttribute('aria-label'));

    expect(labels).toContain('Total tickets: 42');
    expect(labels).toContain('Open: 10');
  });
});
