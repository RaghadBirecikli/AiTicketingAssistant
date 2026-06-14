namespace AiTicketing.Application.Auth;

public sealed record CurrentUserResponse(
    string Id,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> Roles);
