namespace AiTicketing.Application.Ai;

public sealed record TicketAssistantRequest(
    string Title,
    string Description,
    string? CustomerEmail = null);
