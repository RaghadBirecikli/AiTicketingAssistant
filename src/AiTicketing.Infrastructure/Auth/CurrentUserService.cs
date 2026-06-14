using System.Security.Claims;
using AiTicketing.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AiTicketing.Infrastructure.Auth;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? UserId => IsAuthenticated
        ? FindFirstValue(ClaimTypes.NameIdentifier, "sub")
        : null;

    public string? Email => IsAuthenticated
        ? FindFirstValue(ClaimTypes.Email, "email")
        : null;

    public string? FullName => IsAuthenticated
        ? FindFirstValue(ClaimTypes.Name, "name")
        : null;

    public string? Role => IsAuthenticated
        ? FindFirstValue(ClaimTypes.Role, "role")
        : null;

    private string? FindFirstValue(params string[] claimTypes)
    {
        var user = User;
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
