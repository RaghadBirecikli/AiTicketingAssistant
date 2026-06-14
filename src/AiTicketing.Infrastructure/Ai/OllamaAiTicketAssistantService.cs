using System.Net.Http.Json;
using System.Text.Json;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Common.Exceptions;
using AiTicketing.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiTicketing.Infrastructure.Ai;

public sealed class OllamaAiTicketAssistantService(
    HttpClient httpClient,
    IOptions<AiAssistantSettings> options,
    RuleBasedAiTicketAssistantService fallback,
    ILogger<OllamaAiTicketAssistantService> logger,
    IAiOperationTelemetryContext? telemetryContext = null) : IAiTicketAssistantService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = options.Value.Ollama;
            var ollamaRequest = new OllamaGenerateRequest(
                Model: settings.Model,
                Prompt: BuildPrompt(request),
                Stream: false,
                Format: "json");

            using var response = await httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, SerializerOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleAnalyzeFailureAsync(
                    request,
                    $"Ollama returned {(int)response.StatusCode} while analyzing a ticket.",
                    cancellationToken);
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(SerializerOptions, cancellationToken);
            var parsed = TryParseResult(payload?.Response);

            return parsed is null
                ? await HandleAnalyzeFailureAsync(
                    request,
                    "Ollama returned an invalid or empty ticket analysis response.",
                    cancellationToken)
                : MarkOllamaAnalyzeSuccess(parsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return await HandleAnalyzeFailureAsync(
                request,
                "Ollama is unavailable or timed out while analyzing a ticket.",
                cancellationToken,
                ex);
        }
    }

    public async Task<string> SuggestReplyAsync(
        TicketReplySuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = options.Value.Ollama;
            var ollamaRequest = new OllamaGenerateRequest(
                Model: settings.Model,
                Prompt: BuildReplyPrompt(request),
                Stream: false,
                Format: null);

            using var response = await httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await HandleSuggestReplyFailureAsync(
                    request,
                    $"Ollama returned {(int)response.StatusCode} while generating a reply suggestion.",
                    cancellationToken);
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(SerializerOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload?.Response))
            {
                return await HandleSuggestReplyFailureAsync(
                    request,
                    "Ollama returned an invalid or empty reply suggestion.",
                    cancellationToken);
            }

            telemetryContext?.MarkProvider(AiAssistantProviders.Ollama);
            return payload.Response.Trim();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return await HandleSuggestReplyFailureAsync(
                request,
                "Ollama is unavailable or timed out while generating a reply suggestion.",
                cancellationToken,
                ex);
        }
    }

    public async Task<string> SummarizeTicketAsync(
        TicketSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = options.Value.Ollama;
            var ollamaRequest = new OllamaGenerateRequest(
                Model: settings.Model,
                Prompt: BuildSummaryPrompt(request),
                Stream: false,
                Format: null);

            using var response = await httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await HandleSummaryFailureAsync(
                    request,
                    $"Ollama returned {(int)response.StatusCode} while generating a ticket summary.",
                    cancellationToken);
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(SerializerOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload?.Response))
            {
                return await HandleSummaryFailureAsync(
                    request,
                    "Ollama returned an invalid or empty ticket summary.",
                    cancellationToken);
            }

            telemetryContext?.MarkProvider(AiAssistantProviders.Ollama);
            return payload.Response.Trim();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return await HandleSummaryFailureAsync(
                request,
                "Ollama is unavailable or timed out while generating a ticket summary.",
                cancellationToken,
                ex);
        }
    }

    public async Task<TicketTriageSuggestion> SuggestTriageAsync(
        TicketTriageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = options.Value.Ollama;
            var ollamaRequest = new OllamaGenerateRequest(
                Model: settings.Model,
                Prompt: BuildTriagePrompt(request),
                Stream: false,
                Format: "json");

            using var response = await httpClient.PostAsJsonAsync("/api/generate", ollamaRequest, SerializerOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await HandleTriageFailureAsync(
                    request,
                    $"Ollama returned {(int)response.StatusCode} while generating triage suggestions.",
                    cancellationToken);
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(SerializerOptions, cancellationToken);
            var parsed = TryParseTriageSuggestion(payload?.Response);

            if (parsed is null)
            {
                return await HandleTriageFailureAsync(
                    request,
                    "Ollama returned an invalid or empty triage suggestion.",
                    cancellationToken);
            }

            telemetryContext?.MarkProvider(AiAssistantProviders.Ollama);
            return parsed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return await HandleTriageFailureAsync(
                request,
                "Ollama is unavailable or timed out while generating triage suggestions.",
                cancellationToken,
                ex);
        }
    }

    private async Task<TicketAssistantResult> HandleAnalyzeFailureAsync(
        TicketAssistantRequest request,
        string message,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (options.Value.Ollama.FallbackToRuleBased)
        {
            LogProviderFailure($"{message} Falling back to rule-based AI.", exception);
            var result = await fallback.AnalyzeAsync(request, cancellationToken);
            telemetryContext?.MarkFallback(AiAssistantProviders.Ollama);
            return result;
        }

        LogProviderFailure("Ollama failed and rule-based fallback is disabled.", exception);
        throw new AiProviderUnavailableException();
    }

    private async Task<string> HandleSuggestReplyFailureAsync(
        TicketReplySuggestionRequest request,
        string message,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (options.Value.Ollama.FallbackToRuleBased)
        {
            LogProviderFailure($"{message} Falling back to rule-based AI.", exception);
            var result = await fallback.SuggestReplyAsync(request, cancellationToken);
            telemetryContext?.MarkFallback(AiAssistantProviders.Ollama);
            return result;
        }

        LogProviderFailure("Ollama failed and rule-based fallback is disabled.", exception);
        throw new AiProviderUnavailableException();
    }

    private async Task<string> HandleSummaryFailureAsync(
        TicketSummaryRequest request,
        string message,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (options.Value.Ollama.FallbackToRuleBased)
        {
            LogProviderFailure($"{message} Falling back to rule-based AI.", exception);
            var result = await fallback.SummarizeTicketAsync(request, cancellationToken);
            telemetryContext?.MarkFallback(AiAssistantProviders.Ollama);
            return result;
        }

        LogProviderFailure("Ollama failed and rule-based fallback is disabled.", exception);
        throw new AiProviderUnavailableException();
    }

    private async Task<TicketTriageSuggestion> HandleTriageFailureAsync(
        TicketTriageRequest request,
        string message,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (options.Value.Ollama.FallbackToRuleBased)
        {
            LogProviderFailure($"{message} Falling back to rule-based AI.", exception);
            var result = await fallback.SuggestTriageAsync(request, cancellationToken);
            telemetryContext?.MarkFallback(AiAssistantProviders.Ollama);
            return result;
        }

        LogProviderFailure("Ollama failed and rule-based fallback is disabled.", exception);
        throw new AiProviderUnavailableException();
    }

    private void LogProviderFailure(string message, Exception? exception)
    {
        if (exception is null)
        {
            logger.LogWarning("{Message}", message);
            return;
        }

        logger.LogWarning(exception, "{Message}", message);
    }

    private TicketAssistantResult MarkOllamaAnalyzeSuccess(TicketAssistantResult result)
    {
        telemetryContext?.MarkProvider(AiAssistantProviders.Ollama);
        return result with { Provider = AiAssistantProviders.Ollama };
    }

    private static string BuildPrompt(TicketAssistantRequest request) =>
        """
        You are a support ticket triage assistant. Return only valid JSON matching this schema:
        {
          "summary": "short customer issue summary",
          "suggestedCategory": "one concise category",
          "suggestedPriority": "Low, Medium, or High",
          "suggestedReply": "professional support reply",
          "tags": ["short", "lowercase", "tags"]
        }

        Ticket:
        """ +
        $"""

        Title: {request.Title}
        Description: {request.Description}
        CustomerEmail: {request.CustomerEmail}
        """;

    private static string BuildReplyPrompt(TicketReplySuggestionRequest request)
    {
        var conversation = request.Messages.Count == 0
            ? "No customer-visible messages yet."
            : string.Join(Environment.NewLine, request.Messages.Select(message => $"{message.SenderRole}: {message.Body}"));

        return
            """
            You are a customer support assistant. Write only a helpful, professional customer-facing reply.
            Use no markdown unless it genuinely improves clarity. Do not invent facts.
            Do not mention internal notes, AI, internal system details, hidden context, or policies.
            Return only the suggested reply text. Do not include analysis or labels.

            """ +
            $"""
            Ticket title: {request.Title}
            Ticket description: {request.Description}
            Status: {request.Status}
            Priority: {request.Priority}
            Optional instruction: {request.Instruction}

            Customer-visible conversation:
            {conversation}
            """;
    }

    private static string BuildSummaryPrompt(TicketSummaryRequest request)
    {
        var conversation = request.Messages.Count == 0
            ? "No messages yet."
            : string.Join(Environment.NewLine, request.Messages.Select(message =>
                $"{(message.IsInternalNote ? "Internal note" : "Visible message")} | " +
                $"{message.CreatedAtUtc:u} | {message.SenderRole} | {message.SenderDisplayName}: {message.Body}"));

        var internalNoteInstruction = request.IncludesInternalNotes
            ? "Internal notes are included for staff review. Do not expose internal-only content in customer-facing wording."
            : "Internal notes are not included.";

        return
            """
            You are a support operations assistant. Return only a concise, professional plain-text summary for support staff.
            Include: customer issue, important context, troubleshooting or actions already taken, current state, unresolved questions or blockers, and recommended next action.
            Do not invent facts. Do not claim an action occurred unless it is present in the ticket. Clearly distinguish confirmed facts from unresolved items.
            Do not return markdown tables, provider metadata, or the raw prompt.

            """ +
            $"""
            Ticket title: {request.Title}
            Ticket description: {request.Description}
            Status: {request.Status}
            Priority: {request.Priority}
            Created at UTC: {request.CreatedAtUtc:u}
            Assigned agent display name: {request.AssignedAgentDisplayName}
            Internal note handling: {internalNoteInstruction}

            Conversation:
            {conversation}
            """;
    }

    private static string BuildTriagePrompt(TicketTriageRequest request)
    {
        var conversation = request.Messages.Count == 0
            ? "No customer-visible messages yet."
            : string.Join(Environment.NewLine, request.Messages.Select(message =>
                $"{message.CreatedAtUtc:u} | {message.SenderRole} | {message.SenderDisplayName}: {message.Body}"));

        var allowedPriorities = string.Join(", ", Enum.GetNames<TicketPriority>());
        var allowedCategories = string.Join(", ", Enum.GetNames<TicketCategory>());

        return
            """
            You are a support ticket triage assistant. Return only valid JSON matching this schema:
            {
              "suggestedPriority": "one allowed priority",
              "suggestedCategory": "one allowed category",
              "escalationRecommended": false,
              "escalationReason": null,
              "rationale": "concise advisory rationale"
            }

            Requirements:
            - Suggestions are advisory only and must not imply an automatic change.
            - Do not invent facts or claim policy requirements.
            - Do not infer security incidents, legal issues, payment loss, or outages without clear evidence.
            - Use only the allowed priority and category values.
            - Keep rationale concise.
            - Do not include provider metadata, raw prompt text, or markdown.
            - Internal notes are not included and must not be referenced.

            """ +
            $"""
            Allowed priorities: {allowedPriorities}
            Allowed categories: {allowedCategories}

            Ticket title: {request.Title}
            Ticket description: {request.Description}
            Current status: {request.Status}
            Current priority: {request.CurrentPriority}
            Current category: {request.CurrentCategory}
            Created at UTC: {request.CreatedAtUtc:u}
            Assigned agent user id: {request.AssignedAgentUserId}
            Assigned agent display name: {request.AssignedAgentDisplayName}
            Optional instruction: {request.Instruction}

            Customer-visible conversation:
            {conversation}
            """;
    }

    private static TicketAssistantResult? TryParseResult(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<OllamaTicketAssistantResult>(response, SerializerOptions);

        if (result is null ||
            string.IsNullOrWhiteSpace(result.Summary) ||
            string.IsNullOrWhiteSpace(result.SuggestedCategory) ||
            string.IsNullOrWhiteSpace(result.SuggestedPriority) ||
            string.IsNullOrWhiteSpace(result.SuggestedReply))
        {
            return null;
        }

        return new TicketAssistantResult(
            result.Summary,
            result.SuggestedCategory,
            result.SuggestedPriority,
            result.SuggestedReply,
            result.Tags ?? Array.Empty<string>(),
            AiAssistantProviders.Ollama);
    }

    private static TicketTriageSuggestion? TryParseTriageSuggestion(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<OllamaTicketTriageSuggestion>(response, SerializerOptions);
        if (result is null ||
            !TryParseEnum(result.SuggestedPriority, out TicketPriority suggestedPriority) ||
            !TryParseEnum(result.SuggestedCategory, out TicketCategory suggestedCategory))
        {
            return null;
        }

        var categoryText = result.SuggestedCategory?.Trim();
        if (categoryText is null || categoryText.Length > 100)
        {
            return null;
        }

        var rationale = result.Rationale?.Trim();
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        var escalationReason = result.EscalationRecommended
            ? NormalizeWithMaxLength(result.EscalationReason, 500)
            : null;

        if (result.EscalationRecommended && string.IsNullOrWhiteSpace(escalationReason))
        {
            return null;
        }

        return new TicketTriageSuggestion(
            suggestedPriority,
            suggestedCategory,
            result.EscalationRecommended,
            escalationReason,
            rationale);
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeEnumValue(value);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            if (NormalizeEnumValue(enumValue.ToString()) == normalized)
            {
                parsed = enumValue;
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeWithMaxLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > maxLength ? null : normalized;
    }

    private static string NormalizeEnumValue(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream, string? Format);

    private sealed record OllamaGenerateResponse(string? Response);

    private sealed record OllamaTicketAssistantResult(
        string Summary,
        string SuggestedCategory,
        string SuggestedPriority,
        string SuggestedReply,
        string[]? Tags);

    private sealed record OllamaTicketTriageSuggestion(
        string? SuggestedPriority,
        string? SuggestedCategory,
        bool EscalationRecommended,
        string? EscalationReason,
        string? Rationale);
}
