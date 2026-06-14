using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Models;
using AiTicketing.Infrastructure.Auth;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiTicketing.Tests.Auth;

public sealed class AuthControllerTests
{
    private const string ValidPassword = "Password123";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Register_WhenRequestIsValid_ReturnsCreatedAuthResponse()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Admin User",
            email = "admin.registration@example.com",
            password = ValidPassword,
            role = AuthRoles.Admin
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.UserId));
        Assert.Equal("admin.registration@example.com", body.Data.Email);
        Assert.Equal(AuthRoles.Admin, body.Data.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.Token));
        Assert.Equal(TimeSpan.Zero, body.Data.ExpiresAtUtc.Offset);
    }

    [Fact]
    public async Task Register_WhenRoleIsInvalid_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Invalid Role User",
            email = "invalid.role@example.com",
            password = ValidPassword,
            role = "Manager"
        });
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"success\":false", content);
        Assert.Contains("Role must be one of", content);
    }

    [Fact]
    public async Task Register_WhenEmailIsInvalid_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Invalid Email User",
            email = "not-an-email",
            password = ValidPassword,
            role = AuthRoles.Customer
        });
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"success\":false", content);
        Assert.Contains("Email", content);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = new
        {
            fullName = "Duplicate User",
            email = "duplicate@example.com",
            password = ValidPassword,
            role = AuthRoles.Agent
        };

        using var firstResponse = await client.PostAsJsonAsync("/api/auth/register", payload);
        using var secondResponse = await client.PostAsJsonAsync("/api/auth/register", payload);
        var content = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        Assert.Contains("\"success\":false", content);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsAuthResponse()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "Support Agent", "agent.login@example.com", AuthRoles.Agent);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "agent.login@example.com",
            password = ValidPassword
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.Token));
        Assert.Equal(AuthRoles.Agent, body.Data.Role);
        Assert.Equal(TimeSpan.Zero, body.Data.ExpiresAtUtc.Offset);
    }

    [Fact]
    public async Task Login_WhenPasswordIsWrong_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "Support Agent", "wrong.password@example.com", AuthRoles.Agent);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "wrong.password@example.com",
            password = "WrongPassword123"
        });
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"success\":false", content);
        Assert.Contains("Invalid email or password.", content);
    }

    [Fact]
    public async Task Login_WhenEmailIsUnknown_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unknown@example.com",
            password = ValidPassword
        });
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"success\":false", content);
        Assert.Contains("Invalid email or password.", content);
    }

    private static async Task RegisterAsync(HttpClient client, string fullName, string email, string role)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName,
            email,
            password = ValidPassword,
            role
        });

        response.EnsureSuccessStatusCode();
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SeedDemoUsers"] = "false",
                        ["JwtSettings:Issuer"] = "AiTicketingAssistant.Tests",
                        ["JwtSettings:Audience"] = "AiTicketingAssistant.Tests",
                        ["JwtSettings:SecretKey"] = "TEST_SECRET_KEY_FOR_AUTH_CONTROLLER_32_BYTES_MINIMUM",
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
