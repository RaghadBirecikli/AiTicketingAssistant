using System.Security.Claims;
using AiTicketing.Application.Auth;
using AiTicketing.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace AiTicketing.Tests.Auth;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void IsAuthenticated_WhenRequestIsUnauthenticated_ReturnsFalse()
    {
        var service = CreateService(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.Null(service.Email);
        Assert.Null(service.FullName);
        Assert.Null(service.Role);
    }

    [Fact]
    public void UserId_WhenAuthenticated_ReturnsNameIdentifierClaim()
    {
        var service = CreateService(CreateAuthenticatedPrincipal(
            new Claim(ClaimTypes.NameIdentifier, "user-123")));

        Assert.True(service.IsAuthenticated);
        Assert.Equal("user-123", service.UserId);
    }

    [Fact]
    public void Email_WhenAuthenticated_ReturnsEmailClaim()
    {
        var service = CreateService(CreateAuthenticatedPrincipal(
            new Claim(ClaimTypes.Email, "admin@aiticketing.local")));

        Assert.Equal("admin@aiticketing.local", service.Email);
    }

    [Fact]
    public void FullName_WhenAuthenticated_ReturnsNameClaim()
    {
        var service = CreateService(CreateAuthenticatedPrincipal(
            new Claim(ClaimTypes.Name, "Admin User")));

        Assert.Equal("Admin User", service.FullName);
    }

    [Fact]
    public void Role_WhenAuthenticated_ReturnsRoleClaim()
    {
        var service = CreateService(CreateAuthenticatedPrincipal(
            new Claim(ClaimTypes.Role, AuthRoles.Admin)));

        Assert.Equal(AuthRoles.Admin, service.Role);
    }

    [Fact]
    public void Claims_WhenStandardClaimTypesAreMissing_FallsBackToJwtClaimNames()
    {
        var service = CreateService(CreateAuthenticatedPrincipal(
            new Claim("sub", "jwt-user-123"),
            new Claim("email", "jwt@example.com"),
            new Claim("name", "JWT User"),
            new Claim("role", AuthRoles.Agent)));

        Assert.Equal("jwt-user-123", service.UserId);
        Assert.Equal("jwt@example.com", service.Email);
        Assert.Equal("JWT User", service.FullName);
        Assert.Equal(AuthRoles.Agent, service.Role);
    }

    [Fact]
    public void IsAuthenticated_WhenHttpContextIsMissing_ReturnsFalseSafely()
    {
        var service = new CurrentUserService(new HttpContextAccessor());

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.Null(service.Email);
        Assert.Null(service.FullName);
        Assert.Null(service.Role);
    }

    private static CurrentUserService CreateService(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user
        };

        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = httpContext
        });
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
