using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Interfaces;
using AiTicketing.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AiTicketing.Infrastructure.Auth;

public sealed class CurrentUserProfileService(
    ICurrentUserService currentUserService,
    UserManager<ApplicationUser> userManager) : ICurrentUserProfileService
{
    public async Task<CurrentUserResponse?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(currentUserService.UserId);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToArray());
    }
}
