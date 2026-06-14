using AiTicketing.Domain.Enums;

namespace AiTicketing.Application.Ai;

public sealed record TicketTriageRequest(
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority CurrentPriority,
    TicketCategory CurrentCategory,
    DateTimeOffset CreatedAtUtc,
    string? AssignedAgentUserId,
    string? AssignedAgentDisplayName,
    IReadOnlyList<TicketTriageMessage> Messages,
    string? Instruction);

public sealed record TicketTriageMessage(
    string SenderRole,
    string? SenderDisplayName,
    string Body,
    DateTimeOffset CreatedAtUtc);

public sealed record TicketTriageSuggestion(
    TicketPriority SuggestedPriority,
    TicketCategory SuggestedCategory,
    bool EscalationRecommended,
    string? EscalationReason,
    string Rationale);
