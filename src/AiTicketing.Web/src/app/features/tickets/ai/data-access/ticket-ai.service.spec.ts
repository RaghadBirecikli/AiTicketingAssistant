import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiService } from '../../../../core/api/api.service';
import { TicketAiService } from './ticket-ai.service';

describe('TicketAiService', () => {
  let api: jasmine.SpyObj<ApiService>;
  let service: TicketAiService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['post']);

    TestBed.configureTestingModule({
      providers: [
        TicketAiService,
        { provide: ApiService, useValue: api }
      ]
    });

    service = TestBed.inject(TicketAiService);
  });

  it('suggested-reply service uses the exact endpoint and POST body', () => {
    api.post.and.returnValue(of({ suggestedReply: 'Reply' }));

    service.suggestReply('ticket-id', 'Keep it concise').subscribe();

    expect(api.post).toHaveBeenCalledWith('/api/tickets/ticket-id/ai/suggest-reply', {
      instruction: 'Keep it concise'
    });
  });

  it('summary service uses the exact endpoint and POST body', () => {
    api.post.and.returnValue(of({ summary: 'Summary' }));

    service.summarizeTicket('ticket-id', false).subscribe();

    expect(api.post).toHaveBeenCalledWith('/api/tickets/ticket-id/ai/summarize', {
      includeInternalNotes: false
    });
  });

  it('triage service uses the exact endpoint and POST body', () => {
    api.post.and.returnValue(of({
      currentPriority: 'Medium',
      suggestedPriority: 'High',
      suggestedCategory: 'Billing',
      escalationRecommended: true,
      escalationReason: 'Payment blocked',
      rationale: 'Checkout is blocked.'
    }));

    service.suggestTriage('ticket-id', 'Focus on urgency').subscribe();

    expect(api.post).toHaveBeenCalledWith('/api/tickets/ticket-id/ai/suggest-triage', {
      instruction: 'Focus on urgency'
    });
  });
});
