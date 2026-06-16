import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { of, Subject, throwError } from 'rxjs';
import { TicketCreatePageComponent } from './ticket-create-page.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { UserRole } from '../../../../core/models/role.model';
import { TicketListService } from '../../data-access/ticket-list.service';
import { CreateTicketResponse } from '../../models/ticket-list.models';
import { ApiError } from '../../../../core/api/api-error';
import { TicketCreateFormComponent } from '../../components/ticket-create-form/ticket-create-form.component';

const createResponse: CreateTicketResponse = {
  ticket: {
    id: '22222222-2222-4222-8222-222222222222',
    title: 'Payment page is broken',
    description: 'Cannot complete payment.',
    status: 'Open',
    priority: 'High',
    category: 'Billing',
    source: 'Web',
    customerEmail: 'customer@example.com',
    customerName: 'Customer User',
    customerUserId: 'customer-id',
    assignedToUserId: null,
    createdAtUtc: '2026-06-04T12:00:00+00:00',
    updatedAtUtc: null,
    resolvedAtUtc: null,
    closedAtUtc: null
  },
  aiSummary: 'Payment issue',
  suggestedReply: 'Thanks for reaching out.'
};

class AuthServiceStub {
  currentRole = signal<UserRole | null>('Customer');
}

describe('TicketCreatePageComponent', () => {
  let fixture: ComponentFixture<TicketCreatePageComponent>;
  let component: TicketCreatePageComponent;
  let ticketService: jasmine.SpyObj<TicketListService>;
  let router: Router;
  let auth: AuthServiceStub;

  beforeEach(async () => {
    auth = new AuthServiceStub();
    ticketService = jasmine.createSpyObj<TicketListService>('TicketListService', ['createTicket']);
    ticketService.createTicket.and.returnValue(of(createResponse));

    await TestBed.configureTestingModule({
      imports: [TicketCreatePageComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: TicketListService, useValue: ticketService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    fixture = TestBed.createComponent(TicketCreatePageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the create-ticket page and form', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Create ticket');
    expect(text).toContain('Title');
    expect(text).toContain('Description');
  });

  it('successful Customer creation navigates to the returned ticket id', () => {
    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });

    expect(ticketService.createTicket).toHaveBeenCalledWith('Payment issue', 'Cannot complete payment.');
    expect(router.navigateByUrl).toHaveBeenCalledWith(
      '/customer/tickets/22222222-2222-4222-8222-222222222222',
      { replaceUrl: true }
    );
  });

  it('uses role-specific success navigation when reused by another authorized route', () => {
    auth.currentRole.set('Admin');

    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });

    expect(router.navigateByUrl).toHaveBeenCalledWith(
      '/admin/tickets/22222222-2222-4222-8222-222222222222',
      { replaceUrl: true }
    );
  });

  it('prevents duplicate submissions while a request is in flight', () => {
    const pending = new Subject<CreateTicketResponse>();
    ticketService.createTicket.and.returnValue(pending.asObservable());

    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });
    component.createTicket({ title: 'Second click', description: 'Second body.' });

    expect(ticketService.createTicket).toHaveBeenCalledTimes(1);
    expect(component.isSubmitting()).toBeTrue();

    pending.next(createResponse);
    pending.complete();
  });

  it('failed submission does not clear the form', () => {
    ticketService.createTicket.and.returnValue(throwError(() => new ApiError(400, 'validation', 'Bad request')));
    const form = fixture.debugElement.query(By.directive(TicketCreateFormComponent)).componentInstance as TicketCreateFormComponent;
    form.form.controls.title.setValue('Payment issue');
    form.form.controls.description.setValue('Cannot complete payment.');

    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });

    expect(component.errorMessage()).toBe('Please check the ticket details and try again.');
    expect(form.form.controls.title.value).toBe('Payment issue');
    expect(form.form.controls.description.value).toBe('Cannot complete payment.');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('maps safe create-ticket errors', () => {
    ticketService.createTicket.and.returnValue(throwError(() => new ApiError(403, 'forbidden', 'Forbidden')));
    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });
    expect(component.errorMessage()).toBe('You are not allowed to create a ticket.');

    ticketService.createTicket.and.returnValue(throwError(() => new ApiError(409, 'unknown', 'Conflict')));
    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });
    expect(component.errorMessage()).toBe('The ticket could not be created because of a conflicting change.');

    ticketService.createTicket.and.returnValue(throwError(() => new Error('server stack')));
    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });
    expect(component.errorMessage()).toBe('Ticket could not be created. Please try again.');
  });

  it('cancel navigates back to the role ticket list', () => {
    component.cancel();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/customer/tickets');

    auth.currentRole.set('Agent');
    component.cancel();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/agent/tickets');
  });

  it('protects unsaved changes and allows navigation after successful submission', () => {
    const confirmSpy = spyOn(window, 'confirm').and.returnValue(false);
    const form = fixture.debugElement.query(By.directive(TicketCreateFormComponent)).componentInstance as TicketCreateFormComponent;
    form.form.controls.title.setValue('Payment issue');
    form.form.markAsDirty();

    expect(component.canDeactivate()).toBeFalse();
    expect(confirmSpy).toHaveBeenCalled();

    component.createTicket({ title: 'Payment issue', description: 'Cannot complete payment.' });

    expect(component.canDeactivate()).toBeTrue();
  });
});
