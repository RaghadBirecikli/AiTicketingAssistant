using AiTicketing.Application.Auth;
using AiTicketing.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AiTicketing.Infrastructure.Auth;

public static class IdentitySeeder
{
    private const string DemoPassword = "P@ssw0rd!123";

    private static readonly DemoUser[] DemoUsers =
    [
        new("Admin User", "admin@aiticketing.local", AuthRoles.Admin),
        new("Support Agent", "agent@aiticketing.local", AuthRoles.Agent),
        new("Demo Customer", "customer@aiticketing.local", AuthRoles.Customer)
    ];

    public static Task SeedIdentityRolesAsync(this IServiceProvider serviceProvider) =>
        serviceProvider.SeedIdentityAsync(seedDemoUsers: false);

    public static async Task SeedIdentityAsync(this IServiceProvider serviceProvider, bool seedDemoUsers)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await SeedRolesAsync(roleManager);

        if (!seedDemoUsers)
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await SeedDemoUsersAsync(userManager);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in AuthRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                ThrowIfIdentityFailed(result);
            }
        }
    }

    private static async Task SeedDemoUsersAsync(UserManager<ApplicationUser> userManager)
    {
        foreach (var demoUser in DemoUsers)
        {
            var user = await userManager.FindByEmailAsync(demoUser.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = demoUser.Email,
                    Email = demoUser.Email,
                    EmailConfirmed = true,
                    FullName = demoUser.FullName,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };

                var createResult = await userManager.CreateAsync(user, DemoPassword);
                ThrowIfIdentityFailed(createResult);
            }

            if (!await userManager.IsInRoleAsync(user, demoUser.Role))
            {
                var roleResult = await userManager.AddToRoleAsync(user, demoUser.Role);
                ThrowIfIdentityFailed(roleResult);
            }
        }
    }

    private static void ThrowIfIdentityFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Identity seeding failed: {errors}");
    }

    private sealed record DemoUser(string FullName, string Email, string Role);
}
