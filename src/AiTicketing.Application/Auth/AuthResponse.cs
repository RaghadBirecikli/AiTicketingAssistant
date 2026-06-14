namespace AiTicketing.Application.Auth;

public sealed record AuthResponse(
    string UserId,
    string FullName,
    string Email,
    string Role,
    string Token,
    DateTimeOffset ExpiresAtUtc);
