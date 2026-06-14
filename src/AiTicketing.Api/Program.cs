using System.Security.Claims;
using System.Threading.RateLimiting;
using AiTicketing.Api.Filters;
using System.Text.Json.Serialization;
using AiTicketing.Api.Middleware;
using AiTicketing.Application;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Common.Models;
using AiTicketing.Infrastructure;
using AiTicketing.Infrastructure.Auth;
using AiTicketing.Infrastructure.Ai;
using AiTicketing.Infrastructure.Notifications;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

const string DevelopmentCorsPolicy = "DevelopmentFrontend";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevelopmentCorsPolicy, policy =>
        {
            policy
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT token. Swagger will send it as: Authorization: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AiRateLimitPolicyNames.AiEndpoints, httpContext =>
    {
        var settings = httpContext.RequestServices
            .GetRequiredService<IOptions<AiAssistantSettings>>()
            .Value
            .RateLimit;
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            httpContext.User.FindFirstValue("sub") ??
            "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            userId,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
        }

        await RecordRateLimitedAiTelemetryAsync(context.HttpContext, cancellationToken);

        context.HttpContext.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail("Too many AI requests. Please try again later.", []);
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };
});
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevelopmentCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<NotificationHub>(NotificationHubContract.Route);

await app.Services.SeedIdentityAsync(
    app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("SeedDemoUsers"));

app.Run();

static async Task RecordRateLimitedAiTelemetryAsync(HttpContext context, CancellationToken cancellationToken)
{
    var metadata = context.GetEndpoint()?.Metadata.GetMetadata<AiOperationTelemetryAttribute>();
    if (metadata is null)
    {
        return;
    }

    try
    {
        var telemetry = context.RequestServices.GetRequiredService<IAiOperationTelemetry>();
        var settings = context.RequestServices.GetRequiredService<IOptions<AiAssistantSettings>>().Value;
        await telemetry.RecordAsync(new AiOperationTelemetryRecord(
            metadata.OperationName,
            settings.EffectiveProvider,
            AiOperationOutcomes.RateLimited,
            0,
            StatusCodes.Status429TooManyRequests,
            false,
            DateTimeOffset.UtcNow), cancellationToken);
    }
    catch
    {
        // Telemetry must never affect rate-limit responses.
    }
}

public partial class Program;
