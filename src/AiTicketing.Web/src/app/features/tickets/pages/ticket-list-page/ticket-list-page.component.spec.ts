import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { TicketListPageComponent } from './ticket-list-page.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { UserRole } from '../../../../core/models/role.model';
import { TicketListService } from '../../data-access/ticket-list.service';
import { PagedTicketList } from '../../models/ticket-list.models';
import { ApiError } from '../../../../core/api/api-error';

const page: PagedTicketList = {
  items: [
    {
      id: 'ticket-1',
      title: 'Payment page is not working',
      description: 'The customer cannot complete payment.',
      status: 'Open',
      priority: 'High',
      category: 'Billing',
      source: 'Web',
      customerEmail: 'customer@example.com',
      customerName: 'Sara Ahmed',
      customerUserId: 'customer-id',
      assignedToUserId: 'agent-id',
      createdAtUtc: '2026-06-04T12:00:00+00:00',
      updatedAtUtc: null,
      resolvedAtUtc: null,
      closedAtUtc: null
    }
  ],
  page: 1,
  pageSize: 20,
  totalCount: 1,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false
};

class ActivatedRouteStub {
  readonly queryParamMapSubject = new BehaviorSubject<ParamMap>(convertToParamMap({}));
  readonly queryParamMap = this.queryParamMapSubject.asObservable();
  readonly snapshot = { data: { title: 'Tickets' } };
}

class AuthServiceStub {
  currentRole = signal<UserRole | null>('Admin');
  currentUser = signal({
    id: 'admin-id',
    email: 'admin@example.com',
    displayName: 'Admin User',
    roles: ['Admin' as UserRole]
  });
}

describe('TicketListPageComponent', () => {
  let fixture: ComponentFixture<TicketListPageComponent>;
  let route: ActivatedRouteStub;
  let ticketService: jasmine.SpyObj<TicketListService>;
  let router: Router;
  let auth: AuthServiceStub;

  beforeEach(async () => {
    route = new ActivatedRouteStub();
    auth = new AuthServiceStub();
    ticketService = jasmine.createSpyObj<TicketListService>('TicketListService', ['getTickets', 'getAgents']);
    ticketService.getTickets.and.returnValue(of(page));
    ticketService.getAgents.and.returnValue(of([
      { id: 'agent-id', email: 'agent@example.com', displayName: 'Support Agent' }
    ]));

    await TestBed.configureTestingModule({
      imports: [TicketListPageComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: route },
        { provide: AuthService, useValue: auth },
        { provide: TicketListService, useValue: ticketService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  function createComponent(): void {
    fixture = TestBed.createComponent(TicketListPageComponent);
    fixture.detectChanges();
  }

  it('loads Agent options for Admin users only', () => {
    createComponent();

    expect(ticketService.getAgents).toHaveBeenCalled();

    ticketService.getAgents.calls.reset();
    auth.currentRole.set('Agent');
    createComponent();

    expect(ticketService.getAgents).not.toHaveBeenCalled();
  });

  it('renders ticket rows with actual response values and safe local dates', () => {
    createComponent();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Payment page is not working');
    expect(text).toContain('Open');
    expect(text).toContain('High');
    expect(text).toContain('Billing');
    expect(text).toContain('Sara Ahmed');
    expect(text).not.toContain('Invalid date');
  });

  it('uses the same semantic table data for mobile card styling', () => {
    createComponent();

    const firstCell = (fixture.nativeElement as HTMLElement).querySelector('tbody td');
    const cardLink = (fixture.nativeElement as HTMLElement).querySelector('.ticket-card-link');
    const updatedCell = (fixture.nativeElement as HTMLElement).querySelector('.date-cell.empty-metadata');

    expect(firstCell?.getAttribute('data-label')).toBe('Ticket');
    expect(firstCell?.querySelector('a')?.getAttribute('dir')).toBe('auto');
    expect(firstCell?.querySelector('span')?.getAttribute('dir')).toBe('auto');
    expect(cardLink).not.toBeNull();
    expect(updatedCell).not.toBeNull();
  });

  it('toggles mobile filters and counts active filters', () => {
    route.queryParamMapSubject.next(convertToParamMap({
      search: 'payment',
      status: 'Open',
      priority: 'High'
    }));
    createComponent();

    const filterButton = (fixture.nativeElement as HTMLElement).querySelector('.filter-toggle');
    expect(filterButton?.textContent).toContain('Filters');
    expect(filterButton?.textContent).toContain('3');
    expect(fixture.componentInstance.isFiltersOpen()).toBeFalse();

    filterButton?.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(fixture.componentInstance.isFiltersOpen()).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).querySelector('.filters')?.classList).toContain('is-open');
  });

  it('ticket list item navigates to the role details route with return URL state', () => {
    createComponent();
    const link = (fixture.nativeElement as HTMLElement).querySelector('tbody a');

    expect(link?.getAttribute('href')).toContain('/admin/tickets/ticket-1');
    expect(link?.getAttribute('href')).toContain('returnUrl');
  });

  it('restores valid route query parameters and loads with them', () => {
    route.queryParamMapSubject.next(convertToParamMap({
      search: ' payment ',
      status: 'Open',
      priority: 'High',
      page: '2',
      pageSize: '50',
      sortBy: 'createdAt',
      sortDirection: 'asc'
    }));

    createComponent();

    expect(ticketService.getTickets).toHaveBeenCalledWith(jasmine.objectContaining({
      search: 'payment',
      status: 'Open',
      priority: 'High',
      page: 2,
      pageSize: 50,
      sortBy: 'createdAt',
      sortDirection: 'asc'
    }), 'Admin');
  });

  it('applying filters resets page to 1 and trims search', () => {
    route.queryParamMapSubject.next(convertToParamMap({ page: '3' }));
    createComponent();
    fixture.componentInstance.filtersForm.patchValue({
      search: '  login  ',
      status: 'InProgress',
      priority: 'Urgent'
    });

    fixture.componentInstance.applyFilters();

    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: jasmine.objectContaining({
        search: 'login',
        status: 'InProgress',
        priority: 'Urgent',
        page: 1
      })
    }));
  });

  it('reset clears filters', () => {
    createComponent();

    fixture.componentInstance.resetFilters();

    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: {
        page: 1,
        pageSize: 20,
        sortBy: 'createdAt',
        sortDirection: 'desc'
      }
    }));
  });

  it('sorting and page-size changes reset to page 1', () => {
    route.queryParamMapSubject.next(convertToParamMap({ page: '4' }));
    createComponent();

    fixture.componentInstance.changeSort();
    fixture.componentInstance.changePageSize();

    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: jasmine.objectContaining({ page: 1 })
    }));
  });

  it('previous and next buttons respect backend metadata', () => {
    ticketService.getTickets.and.returnValue(of({
      ...page,
      page: 2,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true
    }));
    route.queryParamMapSubject.next(convertToParamMap({ page: '2' }));
    createComponent();

    fixture.componentInstance.previousPage();
    fixture.componentInstance.nextPage();

    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: jasmine.objectContaining({ page: 1 })
    }));
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: jasmine.objectContaining({ page: 3 })
    }));
  });

  it('renders loading, empty, and safe error states', fakeAsync(() => {
    ticketService.getTickets.and.returnValue(of({ ...page, items: [], totalCount: 0, totalPages: 0 }));
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No tickets match');

    ticketService.getTickets.and.returnValue(throwError(() => new ApiError(400, 'validation', 'Raw backend validation')));
    route.queryParamMapSubject.next(convertToParamMap({ search: 'bad' }));
    tick();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Some filters are invalid');
    expect(text).not.toContain('Raw backend validation');
  }));

  it('retry performs another request', () => {
    ticketService.getTickets.and.returnValue(throwError(() => new ApiError(503, 'unavailable', 'Failure')));
    createComponent();
    ticketService.getTickets.calls.reset();

    fixture.componentInstance.retry();

    expect(ticketService.getTickets).toHaveBeenCalled();
  });
});
