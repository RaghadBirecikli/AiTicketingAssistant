using AiTicketing.Application.Auth;
using AiTicketing.Domain.Entities;
using AiTicketing.Infrastructure.Auth;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTicketing.Tests.Auth;

public sealed class IdentitySeederTests
{
    [Fact]
    public async Task SeedIdentityAsync_CreatesRoles()
    {
        await using var services = CreateServices();

        await services.SeedIdentityAsync(seedDemoUsers: false);

        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roleManager.RoleExistsAsync(AuthRoles.Admin));
        Assert.True(await roleManager.RoleExistsAsync(AuthRoles.Agent));
        Assert.True(await roleManager.RoleExistsAsync(AuthRoles.Customer));
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenDevelopmentDemoUsersEnabled_CreatesDemoUsers()
    {
        await using var services = CreateServices();

        await services.SeedIdentityAsync(seedDemoUsers: true);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await AssertDemoUserAsync(userManager, "admin@aiticketing.local", "Admin User", AuthRoles.Admin);
        await AssertDemoUserAsync(userManager, "agent@aiticketing.local", "Support Agent", AuthRoles.Agent);
        await AssertDemoUserAsync(userManager, "customer@aiticketing.local", "Demo Customer", AuthRoles.Customer);
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenProductionDemoUsersDisabled_DoesNotCreateDemoUsers()
    {
        await using var services = CreateServices();

        await services.SeedIdentityAsync(seedDemoUsers: false);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync("admin@aiticketing.local"));
        Assert.Null(await userManager.FindByEmailAsync("agent@aiticketing.local"));
        Assert.Null(await userManager.FindByEmailAsync("customer@aiticketing.local"));
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenDemoUserExists_DoesNotDuplicateOrResetPassword()
    {
        await using var services = CreateServices();

        using (var setupScope = services.CreateScope())
        {
            var setupUserManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "admin@aiticketing.local",
                Email = "admin@aiticketing.local",
                EmailConfirmed = true,
                FullName = "Existing Admin",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                IsActive = true
            };

            var createResult = await setupUserManager.CreateAsync(existingUser, "ExistingPassword123");
            Assert.True(createResult.Succeeded);
        }

        await services.SeedIdentityAsync(seedDemoUsers: true);

        using var verifyScope = services.CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await dbContext.Users
            .Where(user => user.Email == "admin@aiticketing.local")
            .ToListAsync();
        var user = await userManager.FindByEmailAsync("admin@aiticketing.local");

        Assert.Single(users);
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user, "ExistingPassword123"));
        Assert.False(await userManager.CheckPasswordAsync(user, "P@ssw0rd!123"));
        Assert.Equal("Existing Admin", user.FullName);
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenDemoUserExistsWithoutRole_AssignsMissingRole()
    {
        await using var services = CreateServices();

        using (var setupScope = services.CreateScope())
        {
            var setupUserManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "agent@aiticketing.local",
                Email = "agent@aiticketing.local",
                EmailConfirmed = true,
                FullName = "Existing Agent",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                IsActive = true
            };

            var createResult = await setupUserManager.CreateAsync(existingUser, "ExistingPassword123");
            Assert.True(createResult.Succeeded);
        }

        await services.SeedIdentityAsync(seedDemoUsers: true);

        using var verifyScope = services.CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("agent@aiticketing.local");

        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, AuthRoles.Agent));
    }

    private static async Task AssertDemoUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.Equal(fullName, user.FullName);
        Assert.True(user.EmailConfirmed);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(await userManager.IsInRoleAsync(user, role));
        Assert.True(await userManager.CheckPasswordAsync(user, "P@ssw0rd!123"));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }
}
