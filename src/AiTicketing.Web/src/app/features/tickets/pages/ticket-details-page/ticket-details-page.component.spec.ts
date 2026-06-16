import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { TicketDetailsPageComponent } from './ticket-details-page.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { UserRole } from '../../../../core/models/role.model';
import { TicketListService } from '../../data-access/ticket-list.service';
import { TicketDetails } from '../../models/ticket-list.models';
import { ApiError } from '../../../../core/api/api-error';
import { TicketAiService } from '../../ai/data-access/ticket-ai.service';
import { TicketAiPanelComponent } from '../../ai/components/ticket-ai-panel/ticket-ai-panel.component';
import { TicketMessageComposerComponent } from '../../components/ticket-message-composer/ticket-message-composer.component';

const ticketId = '11111111-1111-4111-8111-111111111111';

const details: TicketDetails = {
  id: ticketId,
  title: 'Payment page is not working',
  description: 'Customer cannot complete payment.\nSecond line.',
  status: 'InProgress',
  priority: 'High',
  category: 'Billing',
  source: 'Web',
  customerEmail: 'customer@example.com',
  customerName: 'Sara Ahmed',
  customerUserId: 'customer-id',
  assignedToUserId: 'agent-id',
  createdAtUtc: '2026-06-04T12:00:00+00:00',
  updatedAtUtc: '2026-06-04T12:15:00+00:00',
  resolvedAtUtc: null,
  closedAtUtc: null,
  messages: [
    {
      id: 'message-2',
      ticketId,
      senderUserId: 'agent-id',
      senderRole: 'Agent',
      senderDisplayName: 'Support Agent',
      body: '<strong>Do not render HTML</strong>\nFollow-up line',
      isInternalNote: false,
      createdAtUtc: '2026-06-04T12:20:00+00:00'
    },
    {
      id: 'message-1',
      ticketId,
      senderUserId: 'admin-id',
      senderRole: 'Admin',
      senderDisplayName: 'Admin User',
      body: 'Internal escalation note',
      isInternalNote: true,
      createdAtUtc: '2026-06-04T12:10:00+00:00'
    }
  ]
};

class ActivatedRouteStub {
  snapshot = {
    paramMap: convertToParamMap({ id: ticketId }),
    queryParamMap: convertToParamMap({})
  };
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

describe('TicketDetailsPageComponent', () => {
  let fixture: ComponentFixture<TicketDetailsPageComponent>;
  let route: ActivatedRouteStub;
  let ticketService: jasmine.SpyObj<TicketListService>;
  let ticketAiService: jasmine.SpyObj<TicketAiService>;
  let router: Router;
  let auth: AuthServiceStub;

  beforeEach(async () => {
    route = new ActivatedRouteStub();
    auth = new AuthServiceStub();
    ticketService = jasmine.createSpyObj<TicketListService>('TicketListService', [
      'getTicketById',
      'getTickets',
      'getAgents',
      'addMessage',
      'addInternalNote',
      'assignTicket',
      'changeStatus'
    ]);
    ticketAiService = jasmine.createSpyObj<TicketAiService>('TicketAiService', [
      'suggestReply',
      'summarizeTicket',
      'suggestTriage'
    ]);
    ticketAiService.suggestReply.and.returnValue(of({ suggestedReply: 'AI suggested reply' }));
    ticketAiService.summarizeTicket.and.returnValue(of({ summary: 'AI summary' }));
    ticketAiService.suggestTriage.and.returnValue(of({
      currentPriority: 'Medium',
      suggestedPriority: 'High',
      suggestedCategory: 'Billing',
      escalationRecommended: false,
      escalationReason: null,
      rationale: 'Needs review.'
    }));
    ticketService.getTicketById.and.returnValue(of(details));
    ticketService.addMessage.and.returnValue(of({
      id: 'new-public',
      ticketId,
      senderUserId: 'customer-id',
      senderRole: 'Unknown',
      senderDisplayName: 'Sara Ahmed',
      body: 'New public message',
      isInternalNote: false,
      createdAtUtc: '2026-06-04T12:30:00+00:00'
    }));
    ticketService.addInternalNote.and.returnValue(of({
      id: 'new-note',
      ticketId,
      senderUserId: 'admin-id',
      senderRole: 'Admin',
      senderDisplayName: 'Admin User',
      body: 'New internal note',
      isInternalNote: true,
      createdAtUtc: '2026-06-04T12:35:00+00:00'
    }));
    ticketService.getAgents.and.returnValue(of([
      { id: 'agent-id', email: 'agent@example.com', displayName: 'Support Agent' },
      { id: 'agent-2', email: 'agent2@example.com', displayName: 'Second Agent' }
    ]));
    ticketService.assignTicket.and.returnValue(of({
      ...details,
      assignedToUserId: 'agent-2',
      status: 'InProgress',
      messages: undefined as never
    }));
    ticketService.changeStatus.and.returnValue(of({
      ...details,
      status: 'Resolved',
      messages: undefined as never
    }));

    await TestBed.configureTestingModule({
      imports: [TicketDetailsPageComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: route },
        { provide: AuthService, useValue: auth },
        { provide: TicketListService, useValue: ticketService },
        { provide: TicketAiService, useValue: ticketAiService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
  });

  function createComponent(): void {
    fixture = TestBed.createComponent(TicketDetailsPageComponent);
    fixture.detectChanges();
  }

  it('passes the route ticket id to the service', () => {
    createComponent();

    expect(ticketService.getTicketById).toHaveBeenCalledWith(ticketId);
  });

  it('renders metadata title, description, status, priority, category, assignment, and dates', () => {
    createComponent();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Payment page is not working');
    expect(text).toContain('Customer cannot complete payment.');
    expect(text).toContain('In Progress');
    expect(text).toContain('High');
    expect(text).toContain('Billing');
    expect(text).toContain('Support Agent');
    expect(text).not.toContain('agent-id');
    expect(text).not.toContain('Invalid date');
    expect(text).not.toContain('Admin ticket view');
    expect(text).not.toContain('ADMIN TICKET VIEW');
  });

  it('uses a compact localized back action and native collapsible detail sections', () => {
    createComponent();

    const host = fixture.nativeElement as HTMLElement;
    const backButton = host.querySelector('.back-button') as HTMLButtonElement;
    const sections = Array.from(host.querySelectorAll<HTMLDetailsElement>('.details-section'));

    expect(backButton).not.toBeNull();
    expect(backButton.getAttribute('aria-label')).toBe('Back to tickets');
    expect(backButton.querySelector('app-ui-icon')).not.toBeNull();
    expect(sections.length).toBeGreaterThanOrEqual(3);
    expect(sections.every(section => section.open)).toBeTrue();
    expect(sections.map(section => section.querySelector('summary')?.textContent?.trim())).toContain('AI assistant');
  });

  it('renders public-message composer for Admin, Agent, and Customer', () => {
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Public reply');

    auth.currentRole.set('Agent');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Public reply');

    auth.currentRole.set('Customer');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Public reply');
  });

  it('renders internal-note composer for Admin and Agent but not Customer', () => {
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Customers cannot see internal notes');

    auth.currentRole.set('Agent');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Customers cannot see internal notes');

    auth.currentRole.set('Customer');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Customers cannot see internal notes');
  });

  it('Admin sees assignment control and Agent/Customer do not', () => {
    createComponent();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-ticket-assignment-control')).not.toBeNull();

    auth.currentRole.set('Agent');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-ticket-assignment-control')).toBeNull();

    auth.currentRole.set('Customer');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-ticket-assignment-control')).toBeNull();
  });

  it('Agent lookup is requested only for Admin', () => {
    createComponent();
    expect(ticketService.getAgents).toHaveBeenCalled();

    ticketService.getAgents.calls.reset();
    auth.currentRole.set('Agent');
    createComponent();
    expect(ticketService.getAgents).not.toHaveBeenCalled();
  });

  it('Admin and Agent see status control but Customer does not', () => {
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Current status');

    auth.currentRole.set('Agent');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Current status');

    auth.currentRole.set('Customer');
    createComponent();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Current status');
  });

  it('Admin and Agent see AI panel but Customer sees no AI controls or hints', () => {
    createComponent();
    expect(fixture.debugElement.query(By.directive(TicketAiPanelComponent))).not.toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('AI assistant');

    auth.currentRole.set('Agent');
    createComponent();
    expect(fixture.debugElement.query(By.directive(TicketAiPanelComponent))).not.toBeNull();

    auth.currentRole.set('Customer');
    createComponent();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(fixture.debugElement.query(By.directive(TicketAiPanelComponent))).toBeNull();
    expect(text).not.toContain('AI assistant');
    expect(text).not.toContain('suggested reply');
  });

  it('inserts AI suggested reply only into public composer without submitting it', () => {
    createComponent();

    fixture.componentInstance.insertSuggestedReply('Generated customer reply');
    fixture.detectChanges();

    const publicTextarea = (fixture.nativeElement as HTMLElement).querySelector('#public-message-body') as HTMLTextAreaElement;
    const internalTextarea = (fixture.nativeElement as HTMLElement).querySelector('#internal-message-body') as HTMLTextAreaElement;
    expect(publicTextarea.value).toBe('Generated customer reply');
    expect(internalTextarea.value).toBe('');
    expect(ticketService.addMessage).not.toHaveBeenCalled();
    expect(ticketService.addInternalNote).not.toHaveBeenCalled();
  });

  it('does not overwrite an existing public draft without confirmation', () => {
    createComponent();
    const composers = fixture.debugElement.queryAll(By.directive(TicketMessageComposerComponent));
    const publicComposer = composers[0].componentInstance as TicketMessageComposerComponent;
    publicComposer.setDraft('Existing draft');
    spyOn(window, 'confirm').and.returnValue(false);

    fixture.componentInstance.insertSuggestedReply('Generated reply');
    fixture.detectChanges();

    const publicTextarea = (fixture.nativeElement as HTMLElement).querySelector('#public-message-body') as HTMLTextAreaElement;
    expect(window.confirm).toHaveBeenCalled();
    expect(publicTextarea.value).toBe('Existing draft');
  });

  it('replaces an existing public draft only after confirmation', () => {
    createComponent();
    const composers = fixture.debugElement.queryAll(By.directive(TicketMessageComposerComponent));
    const publicComposer = composers[0].componentInstance as TicketMessageComposerComponent;
    publicComposer.setDraft('Existing draft');
    spyOn(window, 'confirm').and.returnValue(true);

    fixture.componentInstance.insertSuggestedReply('Generated reply');
    fixture.detectChanges();

    const publicTextarea = (fixture.nativeElement as HTMLElement).querySelector('#public-message-body') as HTMLTextAreaElement;
    expect(publicTextarea.value).toBe('Generated reply');
  });

  it('renders unassigned state safely', () => {
    ticketService.getTicketById.and.returnValue(of({ ...details, assignedToUserId: null }));
    createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Unassigned');
  });

  it('renders conversation messages, sender names, roles, and dates chronologically', () => {
    createComponent();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Admin User');
    expect(text).toContain('Admin');
    expect(text).toContain('Support Agent');
    expect(text).toContain('Agent');

    const firstIndex = text.indexOf('Internal escalation note');
    const secondIndex = text.indexOf('Do not render HTML');
    expect(firstIndex).toBeLessThan(secondIndex);
  });

  it('preserves message line breaks safely without rendering HTML', () => {
    createComponent();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('<strong>Do not render HTML</strong>');
    expect(element.querySelector('strong')?.textContent).not.toContain('Do not render HTML');
    expect(getComputedStyle(element.querySelector('.body') as Element).whiteSpace).toBe('pre-wrap');
  });

  it('labels internal notes and does not label public messages', () => {
    createComponent();

    const notes = (fixture.nativeElement as HTMLElement).querySelectorAll('.internal-note');
    expect(notes.length).toBe(1);
    expect(notes[0].textContent).toContain('Internal note');
  });

  it('customer-facing data with no internal notes shows no internal-note UI', () => {
    auth.currentRole.set('Customer');
    ticketService.getTicketById.and.returnValue(of({
      ...details,
      messages: details.messages.filter(message => !message.isInternalNote)
    }));
    createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Internal note');
  });

  it('renders empty conversation state', () => {
    ticketService.getTicketById.and.returnValue(of({ ...details, messages: [] }));
    createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No conversation messages');
  });

  it('renders 404, 403, and generic errors safely', fakeAsync(() => {
    ticketService.getTicketById.and.returnValue(throwError(() => new ApiError(404, 'not-found', 'Raw not found')));
    createComponent();
    tick();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('could not be found or you do not have access');

    ticketService.getTicketById.and.returnValue(throwError(() => new ApiError(403, 'forbidden', 'Raw forbidden')));
    fixture.componentInstance.retry();
    tick();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('do not have permission');

    ticketService.getTicketById.and.returnValue(throwError(() => new ApiError(500, 'unknown', 'Stack trace')));
    fixture.componentInstance.retry();
    tick();
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('could not be loaded');
    expect(text).not.toContain('Stack trace');
  }));

  it('retry requests the ticket again', () => {
    ticketService.getTicketById.and.returnValue(throwError(() => new ApiError(500, 'unknown', 'Failure')));
    createComponent();
    ticketService.getTicketById.calls.reset();

    fixture.componentInstance.retry();

    expect(ticketService.getTicketById).toHaveBeenCalledWith(ticketId);
  });

  it('successful public message clears form and adds returned message without internal label', () => {
    createComponent();

    fixture.componentInstance.addPublicMessage({ mode: 'public', body: '  New public message  ' });
    fixture.detectChanges();

    expect(ticketService.addMessage).toHaveBeenCalledWith(ticketId, 'New public message');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('New public message');
    expect(text).toContain('Message sent.');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.message.internal-note').length).toBe(1);
  });

  it('successful internal note clears form and adds returned message with internal label', () => {
    createComponent();

    fixture.componentInstance.addInternalNote({ mode: 'internal', body: 'New internal note' });
    fixture.detectChanges();

    expect(ticketService.addInternalNote).toHaveBeenCalledWith(ticketId, 'New internal note');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('New internal note');
    expect(text).toContain('Internal note added.');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.message.internal-note').length).toBe(2);
  });

  it('successful assignment updates displayed metadata', () => {
    createComponent();

    fixture.componentInstance.assignTicket({ assignedToUserId: 'agent-2', assignedToDisplayName: 'Second Agent' });
    fixture.detectChanges();

    expect(ticketService.assignTicket).toHaveBeenCalledWith(ticketId, 'agent-2');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Second Agent');
    expect(text).toContain('Ticket assigned.');
    expect(text).not.toContain('agent-2');
  });

  it('successful status change updates displayed status', () => {
    createComponent();

    fixture.componentInstance.changeStatus('Resolved');
    fixture.detectChanges();

    expect(ticketService.changeStatus).toHaveBeenCalledWith(ticketId, 'Resolved');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Resolved');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ticket status updated.');
  });

  it('duplicate assignment and status submission are prevented', () => {
    createComponent();
    fixture.componentInstance.isAssigning.set(true);
    fixture.componentInstance.isChangingStatus.set(true);

    fixture.componentInstance.assignTicket({ assignedToUserId: 'agent-2', assignedToDisplayName: 'Second Agent' });
    fixture.componentInstance.changeStatus('Closed');

    expect(ticketService.assignTicket).not.toHaveBeenCalled();
    expect(ticketService.changeStatus).not.toHaveBeenCalled();
  });

  it('workflow errors render safe messages', () => {
    createComponent();

    ticketService.assignTicket.and.returnValue(throwError(() => new ApiError(400, 'validation', 'Raw validation')));
    fixture.componentInstance.assignTicket({ assignedToUserId: 'agent-2', assignedToDisplayName: 'Second Agent' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('This ticket change is not valid.');

    ticketService.changeStatus.and.returnValue(throwError(() => new ApiError(403, 'forbidden', 'Raw forbidden')));
    fixture.componentInstance.changeStatus('Closed');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You are not allowed to update this ticket.');

    ticketService.changeStatus.and.returnValue(throwError(() => new ApiError(404, 'not-found', 'Raw not found')));
    fixture.componentInstance.changeStatus('Closed');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('could not be found or you do not have access');

    ticketService.changeStatus.and.returnValue(throwError(() => new ApiError(409, 'unknown', 'Stack trace')));
    fixture.componentInstance.changeStatus('Closed');
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('changed by another operation');
    expect(text).not.toContain('Stack trace');
  });

  it('workflow loading does not disable message composers', () => {
    createComponent();
    fixture.componentInstance.isAssigning.set(true);
    fixture.componentInstance.isChangingStatus.set(true);
    fixture.detectChanges();

    const publicTextarea = (fixture.nativeElement as HTMLElement).querySelector('#public-message-body') as HTMLTextAreaElement;
    expect(publicTextarea.disabled).toBeFalse();
  });

  it('new messages remain chronological and duplicate returned messages are ignored', () => {
    ticketService.addMessage.and.returnValue(of({
      id: 'message-1',
      ticketId,
      senderUserId: 'admin-id',
      senderRole: 'Admin',
      senderDisplayName: 'Admin User',
      body: 'Duplicate',
      isInternalNote: false,
      createdAtUtc: '2026-06-04T12:05:00+00:00'
    }));
    createComponent();

    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Duplicate' });
    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Duplicate' });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text.match(/Internal escalation note/g)?.length).toBe(1);
  });

  it('prevents double submission while a composer request is in flight', () => {
    createComponent();
    fixture.componentInstance.isSendingMessage.set(true);

    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Duplicate click' });

    expect(ticketService.addMessage).not.toHaveBeenCalled();
  });

  it('composer errors render safe messages', () => {
    createComponent();

    ticketService.addMessage.and.returnValue(throwError(() => new ApiError(400, 'validation', 'Raw validation')));
    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Bad' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Please check the message and try again.');

    ticketService.addMessage.and.returnValue(throwError(() => new ApiError(403, 'forbidden', 'Raw forbidden')));
    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Forbidden' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('You are not allowed to perform this action.');

    ticketService.addMessage.and.returnValue(throwError(() => new ApiError(404, 'not-found', 'Raw not found')));
    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Missing' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('could not be found or you do not have access');

    ticketService.addMessage.and.returnValue(throwError(() => new ApiError(500, 'unknown', 'Stack trace')));
    fixture.componentInstance.addPublicMessage({ mode: 'public', body: 'Broken' });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('The message could not be sent. Please try again.');
    expect(text).not.toContain('Stack trace');
  });

  it('routes back to role-specific ticket lists', () => {
    createComponent();

    fixture.componentInstance.backToTickets();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/tickets');

    auth.currentRole.set('Agent');
    fixture.componentInstance.backToTickets();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/agent/tickets');

    auth.currentRole.set('Customer');
    fixture.componentInstance.backToTickets();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/customer/tickets');
  });

  it('preserves valid internal return URL query state and rejects unsafe return URLs', () => {
    route.snapshot.queryParamMap = convertToParamMap({ returnUrl: '/admin/tickets?status=Open&page=2' });
    createComponent();

    fixture.componentInstance.backToTickets();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/tickets?status=Open&page=2');

    route.snapshot.queryParamMap = convertToParamMap({ returnUrl: 'https://evil.example/admin/tickets' });
    fixture.componentInstance.backToTickets();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/tickets');
  });

  it('invalid route ids render safe not-found state without making a request', () => {
    route.snapshot.paramMap = convertToParamMap({ id: 'not-a-guid' });
    createComponent();

    expect(ticketService.getTicketById).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('could not be found or you do not have access');
  });
});
