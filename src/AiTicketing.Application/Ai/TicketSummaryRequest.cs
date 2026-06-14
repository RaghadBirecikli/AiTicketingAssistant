namespace AiTicketing.Application.Ai;

public sealed record TicketSummaryRequest(
    string Title,
    string Description,
    string Status,
    string Priority,
    DateTimeOffset CreatedAtUtc,
    string? AssignedAgentDisplayName,
    bool IncludesInternalNotes,
    IReadOnlyList<TicketSummaryMessage> Messages);

public sealed record TicketSummaryMessage(
    string SenderRole,
    string? SenderDisplayName,
    string Body,
    DateTimeOffset CreatedAtUtc,
    bool IsInternalNote);
