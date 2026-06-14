using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Auth.Validation;
using AiTicketing.Domain.Entities;
using AiTicketing.Infrastructure.Auth;
using AiTicketing.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiTicketing.Tests.Auth;

public sealed class AuthServiceTests
{
    private const string ValidPassword = "Password123";

    [Fact]
    public async Task RegisterAsync_WhenRequestIsValid_CreatesUser()
    {
        await using var services = CreateServices();
        await SeedRolesAsync(services);
        var authService = services.GetRequiredService<IAuthService>();

        var response = await authService.RegisterAsync(new RegisterRequest(
            "Sara Ahmed",
            "sara@example.com",
            ValidPassword,
            AuthRoles.Agent));

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("sara@example.com");
        var roles = user is null
            ? Array.Empty<string>()
            : await userManager.GetRolesAsync(user);

        Assert.NotNull(user);
        Assert.Equal("Sara Ahmed", user.FullName);
        Assert.True(user.IsActive);
        Assert.Contains(AuthRoles.Agent, roles);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(AuthRoles.Agent, response.Role);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task RegisterAsync_WhenRoleIsInvalid_ThrowsValidationException()
    {
        await using var services = CreateServices();
        await SeedRolesAsync(services);
        var authService = services.GetRequiredService<IAuthService>();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            authService.RegisterAsync(new RegisterRequest(
                "Sara Ahmed",
                "sara@example.com",
                ValidPassword,
                "Manager")));

        Assert.Contains(exception.Errors, error => error.ErrorMessage.Contains("Role must be one of"));
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsToken()
    {
        await using var services = CreateServices();
        await SeedRolesAsync(services);
        var authService = services.GetRequiredService<IAuthService>();
        await authService.RegisterAsync(new RegisterRequest(
            "Support Agent",
            "agent@example.com",
            ValidPassword,
            AuthRoles.Agent));

        var response = await authService.LoginAsync(new LoginRequest("agent@example.com", ValidPassword));

        Assert.Equal("Support Agent", response.FullName);
        Assert.Equal("agent@example.com", response.Email);
        Assert.Equal(AuthRoles.Agent, response.Role);
        Assert.True(response.ExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsWrong_ThrowsValidationException()
    {
        await using var services = CreateServices();
        await SeedRolesAsync(services);
        var authService = services.GetRequiredService<IAuthService>();
        await authService.RegisterAsync(new RegisterRequest(
            "Support Agent",
            "agent@example.com",
            ValidPassword,
            AuthRoles.Agent));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            authService.LoginAsync(new LoginRequest("agent@example.com", "WrongPassword123")));

        Assert.Contains(exception.Errors, error => error.ErrorMessage == "Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_TokenContainsRoleClaim()
    {
        await using var services = CreateServices();
        await SeedRolesAsync(services);
        var authService = services.GetRequiredService<IAuthService>();
        await authService.RegisterAsync(new RegisterRequest(
            "Admin User",
            "admin@example.com",
            ValidPassword,
            AuthRoles.Admin));

        var response = await authService.LoginAsync(new LoginRequest("admin@example.com", ValidPassword));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);

        Assert.Contains(jwt.Claims, claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role") &&
            claim.Value == AuthRoles.Admin);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

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

        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IOptions<JwtSettings>>(Options.Create(new JwtSettings
        {
            Issuer = "AiTicketingAssistant.Tests",
            Audience = "AiTicketingAssistant.Tests",
            SecretKey = "TEST_SECRET_KEY_FOR_AUTH_SERVICE_32_BYTES_MINIMUM",
            ExpirationMinutes = 60
        }));

        return services.BuildServiceProvider();
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in AuthRoles.All)
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
