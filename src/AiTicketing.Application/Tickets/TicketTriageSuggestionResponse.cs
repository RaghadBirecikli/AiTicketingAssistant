using AiTicketing.Domain.Enums;

namespace AiTicketing.Application.Tickets;

public sealed record TicketTriageSuggestionResponse(
    TicketPriority CurrentPriority,
    TicketPriority SuggestedPriority,
    TicketCategory SuggestedCategory,
    bool EscalationRecommended,
    string? EscalationReason,
    string Rationale);
