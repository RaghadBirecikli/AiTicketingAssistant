using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AiTicketing.Application.Auth;
using AiTicketing.Domain.Entities;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AiTicketing.Tests.Dashboard;

public sealed class DashboardControllerTests
{
    private const string Issuer = "AiTicketingAssistant";
    private const string Audience = "AiTicketingAssistant";
    private const string SecretKey = "REPLACE_WITH_A_PRODUCTION_SECRET_KEY_AT_LEAST_32_BYTES";

    [Fact]
    public async Task GetSummary_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithCustomerToken_ReturnsForbidden()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(AuthRoles.Customer));

        using var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithAgentToken_ReturnsForbidden()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(AuthRoles.Agent));

        using var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithAdminToken_ReturnsOk()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                Title = "Admin dashboard ticket",
                Description = "Dashboard should count this ticket.",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Urgent,
                Category = TicketCategory.TechnicalSupport,
                Source = TicketSource.Web,
                CreatedAtUtc = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(AuthRoles.Admin));

        using var response = await client.GetAsync("/api/dashboard/summary");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":true", content);
        Assert.Contains("\"totalTickets\":1", content);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<ApplicationDbContext>? seed = null)
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    if (seed is not null)
                    {
                        using var serviceProvider = services.BuildServiceProvider();
                        using var scope = serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        seed(dbContext);
                    }
                });
            });
    }

    private static string CreateToken(string role)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, $"{role.ToLowerInvariant()}@example.com"),
            new Claim(ClaimTypes.Email, $"{role.ToLowerInvariant()}@example.com"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, $"{role} User"),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
