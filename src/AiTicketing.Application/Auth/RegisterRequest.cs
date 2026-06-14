namespace AiTicketing.Application.Auth;

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string Role);
