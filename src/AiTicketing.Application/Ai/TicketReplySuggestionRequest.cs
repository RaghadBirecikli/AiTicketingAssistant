namespace AiTicketing.Application.Ai;

public sealed record TicketReplySuggestionRequest(
    string Title,
    string Description,
    string Status,
    string Priority,
    IReadOnlyList<TicketReplySuggestionMessage> Messages,
    string? Instruction);

public sealed record TicketReplySuggestionMessage(
    string SenderRole,
    string Body);
