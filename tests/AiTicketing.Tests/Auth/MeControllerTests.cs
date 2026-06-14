using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Models;
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

public sealed class MeControllerTests
{
    private const string ValidPassword = "Password123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(AuthRoles.Admin)]
    [InlineData(AuthRoles.Agent)]
    [InlineData(AuthRoles.Customer)]
    public async Task Get_WhenAuthenticated_ReturnsCurrentUserProfileAndRoles(string role)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"{role.ToLowerInvariant()}.profile@example.com";
        var auth = await RegisterAsync(client, $"{role} Profile", email, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var response = await client.GetAsync("/api/me");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(auth.UserId, body.Data.Id);
        Assert.Equal(email, body.Data.Email);
        Assert.Equal($"{role} Profile", body.Data.DisplayName);
        Assert.Equal([role], body.Data.Roles);
    }

    [Fact]
    public async Task Get_WhenAnonymous_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ResponseDoesNotExposeSensitiveIdentityFields()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "Safe Profile", "safe.profile@example.com", AuthRoles.Agent);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var response = await client.GetAsync("/api/me");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");

        Assert.True(data.TryGetProperty("id", out _));
        Assert.True(data.TryGetProperty("email", out _));
        Assert.True(data.TryGetProperty("displayName", out _));
        Assert.True(data.TryGetProperty("roles", out _));
        Assert.False(data.TryGetProperty("passwordHash", out _));
        Assert.False(data.TryGetProperty("securityStamp", out _));
        Assert.False(data.TryGetProperty("lockoutEnd", out _));
        Assert.False(data.TryGetProperty("accessFailedCount", out _));
    }

    [Fact]
    public async Task Get_WhenAuthenticatedUserNoLongerExists_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "Deleted Profile", "deleted.profile@example.com", AuthRoles.Customer);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(auth.UserId);
            Assert.NotNull(user);
            var deleteResult = await userManager.DeleteAsync(user);
            Assert.True(deleteResult.Succeeded);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_DocumentsCurrentUserProfileContract()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var endpoint = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/me")
            .GetProperty("get");
        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("CurrentUserResponse")
            .GetProperty("properties");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(endpoint.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(endpoint.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(properties.TryGetProperty("id", out _));
        Assert.True(properties.TryGetProperty("email", out _));
        Assert.True(properties.TryGetProperty("displayName", out _));
        Assert.True(properties.TryGetProperty("roles", out _));
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
                        ["JwtSettings:SecretKey"] = "TEST_SECRET_KEY_FOR_ME_CONTROLLER_32_BYTES_MINIMUM",
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
