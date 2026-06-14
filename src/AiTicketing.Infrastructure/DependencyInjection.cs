using System.Text;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Interfaces;
using AiTicketing.Application.Dashboard;
using AiTicketing.Application.Notifications;
using AiTicketing.Application.Tickets;
using AiTicketing.Application.Users;
using AiTicketing.Infrastructure.Ai;
using AiTicketing.Infrastructure.Auth;
using AiTicketing.Infrastructure.Dashboard;
using AiTicketing.Infrastructure.Notifications;
using AiTicketing.Infrastructure.Persistence;
using AiTicketing.Infrastructure.Tickets;
using AiTicketing.Infrastructure.Users;
using AiTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AiTicketing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiAssistantSettings>()
            .Bind(configuration.GetSection(AiAssistantSettings.SectionName))
            .Validate(settings => settings.IsKnownProvider(), "AiAssistant:Provider must be RuleBased or Ollama.")
            .Validate(
                settings => !IsOllamaSelected(settings) || IsValidOllamaSettings(settings.Ollama),
                "AiAssistant:Ollama settings are invalid.")
            .Validate(
                settings => settings.RateLimit.PermitLimit > 0,
                "AiAssistant:RateLimit:PermitLimit must be greater than zero.")
            .Validate(
                settings => settings.RateLimit.WindowSeconds > 0,
                "AiAssistant:RateLimit:WindowSeconds must be greater than zero.")
            .ValidateOnStart();
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

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

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments(NotificationHubContract.Route))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        services.AddScoped<IAiOperationTelemetryContext, AiOperationTelemetryContext>();
        services.AddSingleton<IAiOperationTelemetry, LoggingAiOperationTelemetry>();
        services.AddScoped<RuleBasedAiTicketAssistantService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUserProfileService, CurrentUserProfileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

        services.AddHttpClient<OllamaAiTicketAssistantService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<AiAssistantSettings>>().Value.Ollama;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        services.AddScoped<IAiTicketAssistantService>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<AiAssistantSettings>>().Value;

            return string.Equals(settings.EffectiveProvider, AiAssistantProviders.RuleBased, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<RuleBasedAiTicketAssistantService>()
                : serviceProvider.GetRequiredService<OllamaAiTicketAssistantService>();
        });

        return services;
    }

    private static bool IsOllamaSelected(AiAssistantSettings settings) =>
        string.Equals(settings.EffectiveProvider, AiAssistantProviders.Ollama, StringComparison.OrdinalIgnoreCase);

    private static bool IsValidOllamaSettings(OllamaSettings settings) =>
        Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(settings.Model) &&
        settings.TimeoutSeconds is >= 1 and <= 300;
}
