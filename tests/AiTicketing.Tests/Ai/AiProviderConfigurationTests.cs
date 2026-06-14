using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Exceptions;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure;
using AiTicketing.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiTicketing.Tests.Ai;

public sealed class AiProviderConfigurationTests
{
    [Fact]
    public void MissingAiConfiguration_SelectsRuleBasedProvider()
    {
        using var provider = CreateInfrastructureProvider(new Dictionary<string, string?>());

        var service = provider.GetRequiredService<IAiTicketAssistantService>();

        Assert.IsType<RuleBasedAiTicketAssistantService>(service);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("RuleBased")]
    [InlineData("rulebased")]
    public void RuleBasedProviderConfiguration_SelectsRuleBasedProvider(string providerName)
    {
        using var provider = CreateInfrastructureProvider(new Dictionary<string, string?>
        {
            ["AiAssistant:Provider"] = providerName
        });

        var service = provider.GetRequiredService<IAiTicketAssistantService>();

        Assert.IsType<RuleBasedAiTicketAssistantService>(service);
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("ollama")]
    public void OllamaProviderConfiguration_SelectsOllamaProvider(string providerName)
    {
        using var provider = CreateInfrastructureProvider(new Dictionary<string, string?>
        {
            ["AiAssistant:Provider"] = providerName,
            ["AiAssistant:Ollama:BaseUrl"] = "http://localhost:11434",
            ["AiAssistant:Ollama:Model"] = "llama3.2",
            ["AiAssistant:Ollama:TimeoutSeconds"] = "15"
        });

        var service = provider.GetRequiredService<IAiTicketAssistantService>();

        Assert.IsType<OllamaAiTicketAssistantService>(service);
    }

    [Fact]
    public void UnknownProvider_FailsClearly()
    {
        using var provider = CreateInfrastructureProvider(new Dictionary<string, string?>
        {
            ["AiAssistant:Provider"] = "Unknown"
        });

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IAiTicketAssistantService>());

        Assert.Contains("AiAssistant:Provider must be RuleBased or Ollama.", exception.Failures);
    }

    [Fact]
    public void MissingRateLimitConfiguration_UsesDocumentedDefaults()
    {
        using var provider = CreateInfrastructureProvider(new Dictionary<string, string?>());

        var settings = provider.GetRequiredService<IOptions<AiAssistantSettings>>().Value;

        Assert.Equal(10, settings.RateLimit.PermitLimit);
        Assert.Equal(60, settings.RateLimit.WindowSeconds);
    }

    [Theory]
    [InlineData("0", "60", "AiAssistant:RateLimit:PermitLimit must be greater than zero.")]
    [InlineData("10", "0", "AiAssistant:RateLimit:WindowSeconds must be greater than zero.")]
    public void InvalidRateLimitConfiguration_FailsClearly(
        string permitLimit,
        string windowSeconds,
        string expectedFailure)
    {
        using var provider = CreateInfrastructureProvider(new Dictionary<string, string?>
        {
            ["AiAssistant:RateLimit:PermitLimit"] = permitLimit,
            ["AiAssistant:RateLimit:WindowSeconds"] = windowSeconds
        });

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<AiAssistantSettings>>().Value);

        Assert.Contains(expectedFailure, exception.Failures);
    }

    [Fact]
    public async Task RuleBasedMode_MakesNoOllamaHttpRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var service = new RuleBasedAiTicketAssistantService();

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.NotEmpty(reply);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RuleBasedSummary_ReturnsDeterministicSummary()
    {
        var service = new RuleBasedAiTicketAssistantService();

        var summary = await service.SummarizeTicketAsync(CreateSummaryRequest());

        Assert.Contains("Customer issue: Payment page is broken.", summary);
        Assert.Contains("Recommended next action", summary);
    }

    [Fact]
    public async Task RuleBasedTriage_ReturnsDeterministicAllowedSuggestions()
    {
        var service = new RuleBasedAiTicketAssistantService();

        var first = await service.SuggestTriageAsync(CreateTriageRequest());
        var second = await service.SuggestTriageAsync(CreateTriageRequest());

        Assert.Equal(first, second);
        Assert.Contains(first.SuggestedPriority, Enum.GetValues<TicketPriority>());
        Assert.Contains(first.SuggestedCategory, Enum.GetValues<TicketCategory>());
        Assert.False(string.IsNullOrWhiteSpace(first.Rationale));
    }

    [Fact]
    public async Task OllamaMode_SendsOneNonStreamingRequestWithConfiguredModelAndContext()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = "  Suggested reply from Ollama.  " }));
        var service = CreateOllamaService(handler, model: "custom-model:latest");
        var request = CreateReplyRequest(
            messages:
            [
                new TicketReplySuggestionMessage("Customer", "Visible customer message")
            ]);

        var reply = await service.SuggestReplyAsync(request);

        Assert.Equal("Suggested reply from Ollama.", reply);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/generate", handler.Requests[0].RequestUri?.AbsolutePath);

        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = payload.RootElement;
        Assert.Equal("custom-model:latest", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());

        var prompt = root.GetProperty("prompt").GetString();
        Assert.Contains("Payment page is broken", prompt);
        Assert.Contains("The customer cannot complete payment.", prompt);
        Assert.Contains("InProgress", prompt);
        Assert.Contains("High", prompt);
        Assert.Contains("Visible customer message", prompt);
        Assert.DoesNotContain("Secret internal note", prompt);
    }

    [Fact]
    public async Task OllamaSummary_SendsOneNonStreamingRequestWithConfiguredModelAndContext()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = "  Operational summary from Ollama.  " }));
        var service = CreateOllamaService(handler, model: "summary-model:latest");

        var summary = await service.SummarizeTicketAsync(CreateSummaryRequest());

        Assert.Equal("Operational summary from Ollama.", summary);
        Assert.Equal(1, handler.RequestCount);

        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = payload.RootElement;
        Assert.Equal("summary-model:latest", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        var prompt = root.GetProperty("prompt").GetString();
        Assert.Contains("Payment page is broken", prompt);
        Assert.Contains("The customer cannot complete payment.", prompt);
        Assert.Contains("InProgress", prompt);
        Assert.Contains("High", prompt);
        Assert.Contains("Customer visible message", prompt);
        Assert.Contains("Customer User", prompt);
        Assert.Contains("internal note", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OllamaTriage_SendsStrictJsonRequestWithConfiguredModelAndVisibleContext()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new
            {
                response = """
                {
                  "suggestedPriority": "Urgent",
                  "suggestedCategory": "Billing",
                  "escalationRecommended": true,
                  "escalationReason": "Repeated checkout failures are blocking payment.",
                  "rationale": "Payment completion is blocked and the customer reports repeated failure."
                }
                """
            }));
        var service = CreateOllamaService(handler, model: "triage-model:latest");

        var suggestion = await service.SuggestTriageAsync(CreateTriageRequest());

        Assert.Equal(TicketPriority.Urgent, suggestion.SuggestedPriority);
        Assert.Equal(TicketCategory.Billing, suggestion.SuggestedCategory);
        Assert.True(suggestion.EscalationRecommended);
        Assert.Equal("Repeated checkout failures are blocking payment.", suggestion.EscalationReason);
        Assert.Equal(1, handler.RequestCount);

        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = payload.RootElement;
        Assert.Equal("triage-model:latest", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("json", root.GetProperty("format").GetString());
        var prompt = root.GetProperty("prompt").GetString();
        Assert.Contains("Payment page is broken", prompt);
        Assert.Contains("The customer cannot complete payment.", prompt);
        Assert.Contains("InProgress", prompt);
        Assert.Contains("High", prompt);
        Assert.Contains("Billing", prompt);
        Assert.Contains("Customer visible message", prompt);
        Assert.Contains("Customer User", prompt);
        Assert.Contains("Focus on urgent escalation.", prompt);
        Assert.DoesNotContain("Staff-only internal note", prompt);
    }

    [Fact]
    public async Task OllamaMode_PropagatesCancellationTokenToHttpRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = "Suggested reply." }));
        var service = CreateOllamaService(handler);
        using var cancellationTokenSource = new CancellationTokenSource();

        await service.SuggestReplyAsync(CreateReplyRequest(), cancellationTokenSource.Token);

        Assert.True(handler.CapturedCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task OllamaSummary_PropagatesCancellationTokenToHttpRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = "Summary." }));
        var service = CreateOllamaService(handler);
        using var cancellationTokenSource = new CancellationTokenSource();

        await service.SummarizeTicketAsync(CreateSummaryRequest(), cancellationTokenSource.Token);

        Assert.True(handler.CapturedCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task OllamaTriage_PropagatesCancellationTokenToHttpRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new
            {
                response = """
                {
                  "suggestedPriority": "High",
                  "suggestedCategory": "Billing",
                  "escalationRecommended": false,
                  "escalationReason": null,
                  "rationale": "The ticket describes payment failure."
                }
                """
            }));
        var service = CreateOllamaService(handler);
        using var cancellationTokenSource = new CancellationTokenSource();

        await service.SuggestTriageAsync(CreateTriageRequest(), cancellationTokenSource.Token);

        Assert.True(handler.CapturedCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task OllamaMode_UsesConfiguredModelAndReturnsTrimmedText()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = "  Trimmed suggested reply.  " }));
        var telemetryContext = new AiOperationTelemetryContext();
        var service = CreateOllamaService(handler, model: "llama3.2", telemetryContext: telemetryContext);

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.Equal("Trimmed suggested reply.", reply);
        Assert.Equal(AiAssistantProviders.Ollama, telemetryContext.ProviderCategory);
        Assert.False(telemetryContext.FallbackUsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOllamaResponse_FallsBackWhenEnabled(string responseText)
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = responseText }));
        var service = CreateOllamaService(handler, fallbackToRuleBased: true);

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.Contains("Thanks for contacting us", reply);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task InvalidOllamaResponse_FallsBackWhenEnabled()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ invalid json")
            });
        var service = CreateOllamaService(handler, fallbackToRuleBased: true);

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.Contains("Thanks for contacting us", reply);
    }

    [Fact]
    public async Task NonSuccessOllamaStatus_FallsBackWhenEnabled()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway));
        var service = CreateOllamaService(handler, fallbackToRuleBased: true);

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.Contains("Thanks for contacting us", reply);
    }

    [Fact]
    public async Task ConnectionFailure_FallsBackWhenEnabled()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new HttpRequestException("Connection failed."));
        var service = CreateOllamaService(handler, fallbackToRuleBased: true);

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.Contains("Thanks for contacting us", reply);
    }

    [Fact]
    public async Task Timeout_FallsBackWhenEnabled()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new TaskCanceledException("Timed out."));
        var service = CreateOllamaService(handler, fallbackToRuleBased: true);

        var reply = await service.SuggestReplyAsync(CreateReplyRequest());

        Assert.Contains("Thanks for contacting us", reply);
    }

    [Fact]
    public async Task ProviderFailure_WhenFallbackDisabled_ThrowsControlledException()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = CreateOllamaService(handler, fallbackToRuleBased: false);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.SuggestReplyAsync(CreateReplyRequest()));
    }

    [Fact]
    public async Task SummaryProviderFailure_WhenFallbackEnabled_ReturnsRuleBasedSummary()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var telemetryContext = new AiOperationTelemetryContext();
        var service = CreateOllamaService(handler, fallbackToRuleBased: true, telemetryContext: telemetryContext);

        var summary = await service.SummarizeTicketAsync(CreateSummaryRequest());

        Assert.Contains("Customer issue: Payment page is broken.", summary);
        Assert.Equal(AiAssistantProviders.Ollama, telemetryContext.ProviderCategory);
        Assert.True(telemetryContext.FallbackUsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ invalid json")]
    [InlineData("{\"suggestedPriority\":\"Severe\",\"suggestedCategory\":\"Billing\",\"escalationRecommended\":false,\"escalationReason\":null,\"rationale\":\"Invalid priority.\"}")]
    public async Task TriageProviderFailure_WhenFallbackEnabled_ReturnsRuleBasedSuggestion(string responseText)
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new { response = responseText }));
        var service = CreateOllamaService(handler, fallbackToRuleBased: true);

        var suggestion = await service.SuggestTriageAsync(CreateTriageRequest());

        Assert.Contains(suggestion.SuggestedPriority, Enum.GetValues<TicketPriority>());
        Assert.Contains(suggestion.SuggestedCategory, Enum.GetValues<TicketCategory>());
        Assert.False(string.IsNullOrWhiteSpace(suggestion.Rationale));
    }

    [Fact]
    public async Task TriageProviderFailure_WhenFallbackDisabled_ThrowsControlledException()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            JsonResponse(new
            {
                response = """
                {
                  "suggestedPriority": "Critical",
                  "suggestedCategory": "Billing",
                  "escalationRecommended": false,
                  "escalationReason": null,
                  "rationale": "Invalid priority."
                }
                """
            }));
        var service = CreateOllamaService(handler, fallbackToRuleBased: false);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.SuggestTriageAsync(CreateTriageRequest()));
    }

    [Fact]
    public async Task SummaryProviderFailure_WhenFallbackDisabled_ThrowsControlledException()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = CreateOllamaService(handler, fallbackToRuleBased: false);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.SummarizeTicketAsync(CreateSummaryRequest()));
    }

    private static ServiceProvider CreateInfrastructureProvider(Dictionary<string, string?> aiConfiguration)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=AiTicketingTests;Trusted_Connection=True;TrustServerCertificate=True",
            ["Jwt:Issuer"] = "AiTicketingAssistant.Tests",
            ["Jwt:Audience"] = "AiTicketingAssistant.Tests",
            ["Jwt:SecretKey"] = "THIS_IS_A_TEST_SECRET_KEY_WITH_AT_LEAST_32_BYTES",
            ["Jwt:ExpirationMinutes"] = "60"
        };

        foreach (var item in aiConfiguration)
        {
            configurationValues[item.Key] = item.Value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private static OllamaAiTicketAssistantService CreateOllamaService(
        RecordingHttpMessageHandler handler,
        string model = "llama3.2",
        bool fallbackToRuleBased = true,
        AiOperationTelemetryContext? telemetryContext = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        var settings = Options.Create(new AiAssistantSettings
        {
            Provider = AiAssistantProviders.Ollama,
            Ollama = new OllamaSettings
            {
                BaseUrl = "http://localhost:11434",
                Model = model,
                TimeoutSeconds = 15,
                FallbackToRuleBased = fallbackToRuleBased
            }
        });

        return new OllamaAiTicketAssistantService(
            httpClient,
            settings,
            new RuleBasedAiTicketAssistantService(telemetryContext ?? new AiOperationTelemetryContext()),
            NullLogger<OllamaAiTicketAssistantService>.Instance,
            telemetryContext);
    }

    private static TicketReplySuggestionRequest CreateReplyRequest(
        IReadOnlyList<TicketReplySuggestionMessage>? messages = null) =>
        new(
            Title: "Payment page is broken",
            Description: "The customer cannot complete payment.",
            Status: "InProgress",
            Priority: "High",
            Messages: messages ?? Array.Empty<TicketReplySuggestionMessage>(),
            Instruction: "Keep it concise.");

    private static TicketSummaryRequest CreateSummaryRequest() =>
        new(
            Title: "Payment page is broken",
            Description: "The customer cannot complete payment.",
            Status: "InProgress",
            Priority: "High",
            CreatedAtUtc: new DateTimeOffset(2026, 6, 7, 10, 0, 0, TimeSpan.Zero),
            AssignedAgentDisplayName: "Agent User",
            IncludesInternalNotes: true,
            Messages:
            [
                new TicketSummaryMessage(
                    AuthRoles.Customer,
                    "Customer User",
                    "Customer visible message",
                    new DateTimeOffset(2026, 6, 7, 10, 5, 0, TimeSpan.Zero),
                    false),
                new TicketSummaryMessage(
                    AuthRoles.Agent,
                    "Agent User",
                    "Staff-only internal note",
                    new DateTimeOffset(2026, 6, 7, 10, 10, 0, TimeSpan.Zero),
                    true)
            ]);

    private static TicketTriageRequest CreateTriageRequest() =>
        new(
            Title: "Payment page is broken",
            Description: "The customer cannot complete payment.",
            Status: TicketStatus.InProgress,
            CurrentPriority: TicketPriority.High,
            CurrentCategory: TicketCategory.Billing,
            CreatedAtUtc: new DateTimeOffset(2026, 6, 7, 10, 0, 0, TimeSpan.Zero),
            AssignedAgentUserId: "agent-user",
            AssignedAgentDisplayName: "Agent User",
            Messages:
            [
                new TicketTriageMessage(
                    AuthRoles.Customer,
                    "Customer User",
                    "Customer visible message",
                    new DateTimeOffset(2026, 6, 7, 10, 5, 0, TimeSpan.Zero))
            ],
            Instruction: "Focus on urgent escalation.");

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount => Requests.Count;

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        public CancellationToken CapturedCancellationToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedCancellationToken = cancellationToken;
            Requests.Add(request);

            if (request.Content is not null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return responseFactory(request);
        }
    }
}
