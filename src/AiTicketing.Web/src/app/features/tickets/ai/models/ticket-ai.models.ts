import { TicketCategory, TicketPriority } from '../../models/ticket-list.models';

export interface SuggestReplyRequest {
  instruction: string | null;
}

export interface SuggestedReplyResponse {
  suggestedReply: string;
}

export interface SummarizeTicketRequest {
  includeInternalNotes: boolean;
}

export interface TicketSummaryResponse {
  summary: string;
}

export interface SuggestTriageRequest {
  instruction: string | null;
}

export interface TicketTriageSuggestionResponse {
  currentPriority: TicketPriority;
  suggestedPriority: TicketPriority;
  suggestedCategory: TicketCategory;
  escalationRecommended: boolean;
  escalationReason: string | null;
  rationale: string;
}
