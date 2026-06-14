import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoints } from '../../../../core/api/api-endpoints';
import { ApiService } from '../../../../core/api/api.service';
import {
  SuggestedReplyResponse,
  SuggestReplyRequest,
  SuggestTriageRequest,
  SummarizeTicketRequest,
  TicketSummaryResponse,
  TicketTriageSuggestionResponse
} from '../models/ticket-ai.models';

@Injectable({ providedIn: 'root' })
export class TicketAiService {
  private readonly api = inject(ApiService);

  suggestReply(ticketId: string, instruction: string | null): Observable<SuggestedReplyResponse> {
    return this.api.post<SuggestReplyRequest, SuggestedReplyResponse>(
      `${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/ai/suggest-reply`,
      { instruction }
    );
  }

  summarizeTicket(ticketId: string, includeInternalNotes: boolean): Observable<TicketSummaryResponse> {
    return this.api.post<SummarizeTicketRequest, TicketSummaryResponse>(
      `${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/ai/summarize`,
      { includeInternalNotes }
    );
  }

  suggestTriage(ticketId: string, instruction: string | null): Observable<TicketTriageSuggestionResponse> {
    return this.api.post<SuggestTriageRequest, TicketTriageSuggestionResponse>(
      `${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/ai/suggest-triage`,
      { instruction }
    );
  }
}
