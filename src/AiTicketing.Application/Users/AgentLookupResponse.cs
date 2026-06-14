namespace AiTicketing.Application.Users;

public sealed record AgentLookupResponse(
    string Id,
    string? Email,
    string? DisplayName);
