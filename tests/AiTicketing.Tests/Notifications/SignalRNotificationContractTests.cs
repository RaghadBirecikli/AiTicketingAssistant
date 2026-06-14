using System.Net;
using AiTicketing.Infrastructure.Notifications;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiTicketing.Tests.Notifications;

public sealed class SignalRNotificationContractTests
{
    [Fact]
    public void NotificationHub_RequiresAuthentication()
    {
        var authorizeAttribute = Assert.Single(
            typeof(NotificationHub).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        Assert.IsType<AuthorizeAttribute>(authorizeAttribute);
    }

    [Fact]
    public async Task NotificationHub_NegotiateWithoutToken_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"{NotificationHubContract.Route}/negotiate?negotiateVersion=1",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void NotificationHubContract_UsesStableRouteAndEventName()
    {
        Assert.Equal("/hubs/notifications", NotificationHubContract.Route);
        Assert.Equal("NotificationReceived", NotificationHubContract.NotificationReceivedEvent);
    }

    [Fact]
    public async Task SignalR_UsesNameIdentifierUserIdProvider()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<IUserIdProvider>();

        Assert.IsType<NameIdentifierUserIdProvider>(provider);
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
                        ["JwtSettings:SecretKey"] = "TEST_SECRET_KEY_FOR_SIGNALR_CONTRACT_32_BYTES_MINIMUM",
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
