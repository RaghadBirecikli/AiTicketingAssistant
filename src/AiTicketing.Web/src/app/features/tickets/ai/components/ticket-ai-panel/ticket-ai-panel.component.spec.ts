import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ApiError } from '../../../../../core/api/api-error';
import { LocalizationService } from '../../../../../core/localization/localization.service';
import { ClipboardService } from '../../../../../shared/services/clipboard.service';
import { TicketAiService } from '../../data-access/ticket-ai.service';
import { TicketAiPanelComponent } from './ticket-ai-panel.component';

describe('TicketAiPanelComponent', () => {
  let fixture: ComponentFixture<TicketAiPanelComponent>;
  let component: TicketAiPanelComponent;
  let aiService: jasmine.SpyObj<TicketAiService>;
  let clipboard: jasmine.SpyObj<ClipboardService>;
  let localization: LocalizationService;

  beforeEach(async () => {
    aiService = jasmine.createSpyObj<TicketAiService>('TicketAiService', [
      'suggestReply',
      'summarizeTicket',
      'suggestTriage'
    ]);
    clipboard = jasmine.createSpyObj<ClipboardService>('ClipboardService', ['copyText']);

    aiService.suggestReply.and.returnValue(of({ suggestedReply: 'Reply\nLine two' }));
    aiService.summarizeTicket.and.returnValue(of({ summary: 'Summary\nLine two' }));
    aiService.suggestTriage.and.returnValue(of({
      currentPriority: 'Medium',
      suggestedPriority: 'High',
      suggestedCategory: 'Billing',
      escalationRecommended: true,
      escalationReason: 'Payment blocked',
      rationale: 'Checkout is blocked.'
    }));
    clipboard.copyText.and.resolveTo();

    await TestBed.configureTestingModule({
      imports: [TicketAiPanelComponent],
      providers: [
        { provide: TicketAiService, useValue: aiService },
        { provide: ClipboardService, useValue: clipboard }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketAiPanelComponent);
    localization = TestBed.inject(LocalizationService);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('ticketId', 'ticket-id');
    fixture.componentRef.setInput('role', 'Admin');
    fixture.detectChanges();
  });

  afterEach(() => {
    localization.setLanguage('en');
    localStorage.clear();
    document.documentElement.lang = 'en';
    document.documentElement.dir = 'ltr';
  });

  it('trims suggested-reply instruction and sends null for whitespace-only instruction', () => {
    component.replyForm.controls.instruction.setValue('  Keep it friendly  ');
    component.suggestReply();
    expect(aiService.suggestReply).toHaveBeenCalledWith('ticket-id', 'Keep it friendly');

    aiService.suggestReply.calls.reset();
    component.replyForm.controls.instruction.setValue('   ');
    component.suggestReply();
    expect(aiService.suggestReply).toHaveBeenCalledWith('ticket-id', null);
  });

  it('rejects overlong suggested-reply and triage instructions client-side', () => {
    component.replyForm.controls.instruction.setValue('x'.repeat(component.instructionMaxLength + 1));
    component.suggestReply();

    component.triageForm.controls.instruction.setValue('x'.repeat(component.instructionMaxLength + 1));
    component.suggestTriage();

    expect(aiService.suggestReply).not.toHaveBeenCalled();
    expect(aiService.suggestTriage).not.toHaveBeenCalled();
  });

  it('trims triage instruction and sends null for whitespace-only instruction', () => {
    component.triageForm.controls.instruction.setValue('  Focus on urgency  ');
    component.suggestTriage();
    expect(aiService.suggestTriage).toHaveBeenCalledWith('ticket-id', 'Focus on urgency');

    aiService.suggestTriage.calls.reset();
    component.triageForm.controls.instruction.setValue('   ');
    component.suggestTriage();
    expect(aiService.suggestTriage).toHaveBeenCalledWith('ticket-id', null);
  });

  it('prevents duplicate submissions for each AI operation', () => {
    aiService.suggestReply.and.returnValue(new Subject<{ suggestedReply: string }>());
    aiService.summarizeTicket.and.returnValue(new Subject<{ summary: string }>());
    aiService.suggestTriage.and.returnValue(new Subject<{
      currentPriority: 'Medium';
      suggestedPriority: 'High';
      suggestedCategory: 'Billing';
      escalationRecommended: false;
      escalationReason: null;
      rationale: string;
    }>());

    component.suggestReply();
    component.suggestReply();
    component.summarize();
    component.summarize();
    component.suggestTriage();
    component.suggestTriage();

    expect(aiService.suggestReply).toHaveBeenCalledTimes(1);
    expect(aiService.summarizeTicket).toHaveBeenCalledTimes(1);
    expect(aiService.suggestTriage).toHaveBeenCalledTimes(1);
  });

  it('keeps each AI operation state independent', () => {
    const replySubject = new Subject<{ suggestedReply: string }>();
    aiService.suggestReply.and.returnValue(replySubject);

    component.suggestReply();
    component.summarize();

    expect(component.replyState()).toBe('loading');
    expect(component.summaryState()).toBe('success');
    expect(component.summary()).toBe('Summary\nLine two');
  });

  it('renders suggested reply and summary as plain text without innerHTML', () => {
    aiService.suggestReply.and.returnValue(of({ suggestedReply: '<strong>Reply</strong>\nLine' }));
    aiService.summarizeTicket.and.returnValue(of({ summary: '<em>Summary</em>\nLine' }));

    component.suggestReply();
    fixture.detectChanges();

    let element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('<strong>Reply</strong>');
    expect(element.querySelector('strong')).toBeNull();

    component.summarize();
    fixture.detectChanges();

    element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('<em>Summary</em>');
    expect(element.querySelector('em')).toBeNull();
  });

  it('localizes AI-owned labels while preserving generated output direction', () => {
    localization.setLanguage('ar');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('المساعد الذكي');
    expect(element.textContent).toContain('الرد');

    component.suggestReply();
    fixture.detectChanges();

    const output = element.querySelector('pre');
    expect(output?.getAttribute('dir')).toBe('auto');
    expect(output?.textContent).toContain('Reply');
  });

  it('renders triage fields with textual escalation and without an Apply action', () => {
    component.suggestTriage();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Current priority');
    expect(text).toContain('Medium');
    expect(text).toContain('Suggested priority');
    expect(text).toContain('High');
    expect(text).toContain('Billing');
    expect(text).toContain('Yes');
    expect(text).toContain('Payment blocked');
    expect(text).not.toContain('Apply');
  });

  it('hides escalation reason when it is not supplied', () => {
    aiService.suggestTriage.and.returnValue(of({
      currentPriority: 'Low',
      suggestedPriority: 'Medium',
      suggestedCategory: 'General',
      escalationRecommended: false,
      escalationReason: null,
      rationale: 'Routine issue.'
    }));

    component.suggestTriage();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No');
    expect(text).not.toContain('Escalation reason');
  });

  it('shows include-internal-notes only to Admin and defaults it to false', () => {
    expect(component.summaryForm.controls.includeInternalNotes.value).toBeFalse();
    component.setActiveTab('summary');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Include internal notes');

    fixture.componentRef.setInput('role', 'Agent');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Include internal notes');
  });

  it('Agent summary request never sends includeInternalNotes true', () => {
    fixture.componentRef.setInput('role', 'Agent');
    component.summaryForm.controls.includeInternalNotes.setValue(true);
    component.summarize();

    expect(aiService.summarizeTicket).toHaveBeenCalledWith('ticket-id', false);
  });

  it('emits suggested reply only when user chooses insert', () => {
    spyOn(component.insertReply, 'emit');
    component.suggestReply();

    expect(component.insertReply.emit).not.toHaveBeenCalled();

    component.insertSuggestedReply();

    expect(component.insertReply.emit).toHaveBeenCalledWith('Reply\nLine two');
  });

  it('copies selected AI output and renders safe copy failure feedback', fakeAsync(() => {
    component.suggestReply();
    component.copyReply();
    tick();

    expect(clipboard.copyText).toHaveBeenCalledWith('Reply\nLine two');
    expect(component.replyCopyMessage()).toBe('Copied.');

    clipboard.copyText.and.rejectWith(new Error('denied'));
    component.copyReply();
    tick();

    expect(component.replyCopyMessage()).toBe('The AI output could not be copied.');
  }));

  it('clearing one result does not clear other AI results', () => {
    component.suggestReply();
    component.summarize();
    component.suggestTriage();

    component.clearReply();

    expect(component.suggestedReply()).toBeNull();
    expect(component.summary()).toBe('Summary\nLine two');
    expect(component.triage()).not.toBeNull();
  });

  it('renders safe AI error messages for controlled statuses', () => {
    const cases: Array<[ApiError, string]> = [
      [new ApiError(400, 'validation', 'raw'), 'Please check the AI request and try again.'],
      [new ApiError(403, 'forbidden', 'raw'), 'You are not allowed to use this AI action.'],
      [new ApiError(404, 'not-found', 'raw'), 'The ticket could not be found or you do not have access to it.'],
      [new ApiError(429, 'rate-limited', 'raw', [], 12), 'Too many AI requests. Please wait 12 seconds before trying again.'],
      [new ApiError(503, 'unavailable', 'raw'), 'The AI service is temporarily unavailable. Please try again later.'],
      [new ApiError(500, 'unknown', 'Stack trace'), 'The AI request could not be completed. Please try again.']
    ];

    for (const [error, message] of cases) {
      aiService.suggestReply.and.returnValue(throwError(() => error));
      component.suggestReply();
      expect(component.replyError()).toBe(message);
    }
  });
});
