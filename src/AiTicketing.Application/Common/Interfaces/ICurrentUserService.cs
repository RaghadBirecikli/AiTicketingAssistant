namespace AiTicketing.Application.Common.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? Email { get; }

    string? FullName { get; }

    string? Role { get; }
}
