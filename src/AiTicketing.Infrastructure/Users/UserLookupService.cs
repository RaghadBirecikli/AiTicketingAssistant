using AiTicketing.Application.Auth;
using AiTicketing.Application.Users;
using AiTicketing.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AiTicketing.Infrastructure.Users;

public sealed class UserLookupService(UserManager<ApplicationUser> userManager) : IUserLookupService
{
    public async Task<IReadOnlyList<AgentLookupResponse>> GetAgentsAsync(
        CancellationToken cancellationToken = default)
    {
        var agents = await userManager.GetUsersInRoleAsync(AuthRoles.Agent);

        return agents
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName ?? user.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(user => new AgentLookupResponse(user.Id, user.Email, user.FullName))
            .ToArray();
    }

    public async Task<AgentLookupResponse?> GetActiveAgentByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var isAgent = await userManager.IsInRoleAsync(user, AuthRoles.Agent);
        return isAgent
            ? new AgentLookupResponse(user.Id, user.Email, user.FullName)
            : null;
    }
}
