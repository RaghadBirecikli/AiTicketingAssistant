using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Models;
using AiTicketing.Application.Users;
using AiTicketing.Domain.Entities;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiTicketing.Tests.Auth;

public sealed class UsersControllerTests
{
    private const string ValidPassword = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetAgents_WhenAdmin_ReturnsOnlyActiveAgentsInStableOrder()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var admin = await RegisterAsync(client, "Calling Admin", "calling.admin@example.com", AuthRoles.Admin);
        await RegisterAsync(client, "Beta Agent", "beta.agent@example.com", AuthRoles.Agent);
        await RegisterAsync(client, "Alpha Agent", "alpha.agent@example.com", AuthRoles.Agent);
        await RegisterAsync(client, "Same Agent", "z.same.agent@example.com", AuthRoles.Agent);
        await RegisterAsync(client, "Same Agent", "a.same.agent@example.com", AuthRoles.Agent);
        var inactiveAgent = await RegisterAsync(
            client,
            "Inactive Agent",
            "inactive.agent@example.com",
            AuthRoles.Agent);
        await RegisterAsync(client, "Other Admin", "other.admin@example.com", AuthRoles.Admin);
        await RegisterAsync(client, "Customer User", "customer.lookup@example.com", AuthRoles.Customer);
        await SetUserActiveAsync(factory, inactiveAgent.UserId, isActive: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        using var response = await client.GetAsync("/api/users/agents");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AgentLookupResponse>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(
            ["alpha.agent@example.com", "beta.agent@example.com", "a.same.agent@example.com", "z.same.agent@example.com"],
            body.Data.Select(agent => agent.Email).ToArray());
        Assert.DoesNotContain(body.Data, agent => agent.Email == "inactive.agent@example.com");
        Assert.DoesNotContain(body.Data, agent => agent.Email == "other.admin@example.com");
        Assert.DoesNotContain(body.Data, agent => agent.Email == "customer.lookup@example.com");
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Agent, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Customer, HttpStatusCode.Forbidden)]
    public async Task GetAgents_WhenCallerIsNotAdmin_ReturnsExpectedStatus(
        string? role,
        HttpStatusCode expectedStatusCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        if (role is not null)
        {
            var auth = await RegisterAsync(
                client,
                $"{role} Caller",
                $"{role.ToLowerInvariant()}.lookup.caller@example.com",
                role);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        }

        using var response = await client.GetAsync("/api/users/agents");

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task GetAgents_ResponseUsesStablePublicFieldsOnly()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var admin = await RegisterAsync(client, "Admin User", "admin.public.lookup@example.com", AuthRoles.Admin);
        await RegisterAsync(client, "Public Agent", "public.agent@example.com", AuthRoles.Agent);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        using var response = await client.GetAsync("/api/users/agents");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var agent = document.RootElement.GetProperty("data").EnumerateArray().Single();

        Assert.True(agent.TryGetProperty("id", out _));
        Assert.Equal("public.agent@example.com", agent.GetProperty("email").GetString());
        Assert.Equal("Public Agent", agent.GetProperty("displayName").GetString());
        Assert.False(agent.TryGetProperty("passwordHash", out _));
        Assert.False(agent.TryGetProperty("securityStamp", out _));
        Assert.False(agent.TryGetProperty("lockoutEnd", out _));
        Assert.False(agent.TryGetProperty("accessFailedCount", out _));
    }

    [Fact]
    public async Task Swagger_DocumentsAgentsLookupContract()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var endpoint = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/users/agents")
            .GetProperty("get");
        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("AgentLookupResponse")
            .GetProperty("properties");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(endpoint.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(endpoint.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(endpoint.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(properties.TryGetProperty("id", out _));
        Assert.True(properties.TryGetProperty("email", out _));
        Assert.True(properties.TryGetProperty("displayName", out _));
        Assert.False(properties.TryGetProperty("passwordHash", out _));
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string fullName, string email, string role)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName,
            email,
            password = ValidPassword,
            role
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);

        response.EnsureSuccessStatusCode();
        return Assert.IsType<AuthResponse>(body?.Data);
    }

    private static async Task SetUserActiveAsync(
        WebApplicationFactory<Program> factory,
        string userId,
        bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        Assert.NotNull(user);
        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        Assert.True(result.Succeeded);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Production")
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SeedDemoUsers"] = "false",
                        ["JwtSettings:Issuer"] = "AiTicketingAssistant.Tests",
                        ["JwtSettings:Audience"] = "AiTicketingAssistant.Tests",
                        ["JwtSettings:SecretKey"] = "TEST_SECRET_KEY_FOR_USERS_CONTROLLER_32_BYTES_MINIMUM",
                        ["JwtSettings:ExpirationMinutes"] = "60"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                });
            });
    }
}
