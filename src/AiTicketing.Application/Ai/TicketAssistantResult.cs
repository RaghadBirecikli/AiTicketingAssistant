namespace AiTicketing.Application.Ai;

public sealed record TicketAssistantResult(
    string Summary,
    string SuggestedCategory,
    string SuggestedPriority,
    string SuggestedReply,
    IReadOnlyCollection<string> Tags,
    string Provider);
