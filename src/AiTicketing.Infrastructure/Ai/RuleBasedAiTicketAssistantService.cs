using AiTicketing.Application.Ai;
using AiTicketing.Domain.Enums;

namespace AiTicketing.Infrastructure.Ai;

public sealed class RuleBasedAiTicketAssistantService : IAiTicketAssistantService
{
    private readonly IAiOperationTelemetryContext? telemetryContext;

    public RuleBasedAiTicketAssistantService()
    {
    }

    public RuleBasedAiTicketAssistantService(IAiOperationTelemetryContext telemetryContext)
    {
        this.telemetryContext = telemetryContext;
    }

    public Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default)
    {
        telemetryContext?.MarkProvider(AiAssistantProviders.RuleBased);
        var text = $"{request.Title} {request.Description}".ToLowerInvariant();
        var category = ResolveCategory(text);
        var priority = ResolvePriority(text);
        var tags = ResolveTags(text, category, priority);

        var result = new TicketAssistantResult(
            Summary: BuildSummary(request),
            SuggestedCategory: category,
            SuggestedPriority: priority,
            SuggestedReply: "Thanks for reaching out. We have received your request and will review the details shortly. If you have screenshots, error messages, or recent steps that caused the issue, please add them to help us investigate faster.",
            Tags: tags,
            Provider: AiAssistantProviders.RuleBased);

        return Task.FromResult(result);
    }

    public Task<string> SuggestReplyAsync(
        TicketReplySuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        telemetryContext?.MarkProvider(AiAssistantProviders.RuleBased);
        var instruction = string.IsNullOrWhiteSpace(request.Instruction)
            ? null
            : $" We will also follow your preference: {request.Instruction.Trim()}.";
        var reply =
            $"Thanks for contacting us about \"{request.Title}\". We have reviewed the details and your ticket is currently {request.Status}. " +
            "Our support team will continue working on this and share the next update as soon as possible." +
            instruction;

        return Task.FromResult(reply);
    }

    public Task<string> SummarizeTicketAsync(
        TicketSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        telemetryContext?.MarkProvider(AiAssistantProviders.RuleBased);
        var visibleMessages = request.Messages
            .Where(message => !message.IsInternalNote)
            .ToArray();
        var internalNoteCount = request.Messages.Count(message => message.IsInternalNote);
        var latestVisibleMessage = visibleMessages.LastOrDefault();
        var summary =
            $"Customer issue: {request.Title}. {request.Description.Trim()} " +
            $"Current state: {request.Status} priority ticket with {visibleMessages.Length} visible message(s)." +
            (latestVisibleMessage is null
                ? " No customer-visible conversation has been added yet."
                : $" Latest visible update from {latestVisibleMessage.SenderDisplayName ?? latestVisibleMessage.SenderRole}: {latestVisibleMessage.Body}") +
            (request.IncludesInternalNotes
                ? $" Internal context included for staff review: {internalNoteCount} internal note(s)."
                : " Internal notes were not included.") +
            " Recommended next action: review the ticket details, confirm any missing information, and provide the next customer update.";

        return Task.FromResult(summary);
    }

    public Task<TicketTriageSuggestion> SuggestTriageAsync(
        TicketTriageRequest request,
        CancellationToken cancellationToken = default)
    {
        telemetryContext?.MarkProvider(AiAssistantProviders.RuleBased);
        var text = BuildTriageText(request);
        var suggestedPriority = ResolveTriagePriority(text, request.CurrentPriority);
        var suggestedCategory = ResolveTriageCategory(text, request.CurrentCategory);
        var escalationRecommended = ShouldRecommendEscalation(text, suggestedPriority);
        var escalationReason = escalationRecommended
            ? BuildEscalationReason(text)
            : null;
        var rationale = BuildTriageRationale(request, suggestedPriority, suggestedCategory, escalationRecommended);

        return Task.FromResult(new TicketTriageSuggestion(
            suggestedPriority,
            suggestedCategory,
            escalationRecommended,
            escalationReason,
            rationale));
    }

    private static string ResolveCategory(string text)
    {
        if (ContainsAny(text, "login", "password", "mfa", "authentication", "account"))
        {
            return "Account Access";
        }

        if (ContainsAny(text, "payment", "invoice", "billing", "subscription", "refund"))
        {
            return "Billing";
        }

        if (ContainsAny(text, "error", "bug", "crash", "broken", "exception", "failed"))
        {
            return "Technical Support";
        }

        return "General Support";
    }

    private static string ResolvePriority(string text)
    {
        if (ContainsAny(text, "production down", "outage", "urgent", "critical", "cannot access", "security"))
        {
            return "High";
        }

        if (ContainsAny(text, "blocked", "failed", "error", "refund", "billing"))
        {
            return "Medium";
        }

        return "Low";
    }

    private static TicketPriority ResolveTriagePriority(string text, TicketPriority currentPriority)
    {
        if (ContainsAny(text, "production down", "system down", "outage", "critical", "security incident"))
        {
            return TicketPriority.Urgent;
        }

        if (ContainsAny(text, "urgent", "cannot complete payment", "payment failed", "blocked", "cannot access"))
        {
            return IsLowerPriorityThan(currentPriority, TicketPriority.High) ? TicketPriority.High : currentPriority;
        }

        if (ContainsAny(text, "error", "failed", "bug", "refund", "billing"))
        {
            return IsLowerPriorityThan(currentPriority, TicketPriority.Medium) ? TicketPriority.Medium : currentPriority;
        }

        return currentPriority;
    }

    private static TicketCategory ResolveTriageCategory(string text, TicketCategory currentCategory)
    {
        if (ContainsAny(text, "payment", "invoice", "billing", "subscription", "refund"))
        {
            return TicketCategory.Billing;
        }

        if (ContainsAny(text, "feature", "enhancement", "request"))
        {
            return TicketCategory.FeatureRequest;
        }

        if (ContainsAny(text, "bug", "crash", "exception", "broken"))
        {
            return TicketCategory.Bug;
        }

        if (ContainsAny(text, "login", "password", "mfa", "authentication", "error", "failed"))
        {
            return TicketCategory.TechnicalSupport;
        }

        if (ContainsAny(text, "complaint", "unhappy", "angry", "poor service"))
        {
            return TicketCategory.Complaint;
        }

        return currentCategory;
    }

    private static bool ShouldRecommendEscalation(string text, TicketPriority suggestedPriority) =>
        suggestedPriority == TicketPriority.Urgent ||
        ContainsAny(text, "production down", "system down", "outage", "security incident");

    private static string BuildEscalationReason(string text)
    {
        if (ContainsAny(text, "production down", "system down", "outage"))
        {
            return "The ticket text indicates a possible outage or unavailable production workflow.";
        }

        if (text.Contains("security incident", StringComparison.Ordinal))
        {
            return "The ticket text explicitly references a possible security incident.";
        }

        return "The ticket context contains strong urgency indicators that may need senior review.";
    }

    private static string BuildTriageRationale(
        TicketTriageRequest request,
        TicketPriority suggestedPriority,
        TicketCategory suggestedCategory,
        bool escalationRecommended)
    {
        var priorityText = suggestedPriority == request.CurrentPriority
            ? $"retains the current {request.CurrentPriority} priority"
            : $"suggests moving priority from {request.CurrentPriority} to {suggestedPriority}";
        var escalationText = escalationRecommended
            ? " Escalation is recommended based on strong impact indicators."
            : " Escalation is not recommended from the available context.";

        return $"The triage {priorityText} and classifies the issue as {suggestedCategory} based on the ticket text and visible conversation.{escalationText}";
    }

    private static string BuildTriageText(TicketTriageRequest request) =>
        string.Join(
            ' ',
            new[]
            {
                request.Title,
                request.Description,
                request.Instruction ?? string.Empty
            }.Concat(request.Messages.Select(message => message.Body)))
            .ToLowerInvariant();

    private static bool IsLowerPriorityThan(TicketPriority current, TicketPriority target) =>
        (int)current < (int)target;

    private static string BuildSummary(TicketAssistantRequest request)
    {
        var source = string.IsNullOrWhiteSpace(request.Description) ? request.Title : request.Description;
        var trimmed = source.Trim();

        return trimmed.Length <= 180 ? trimmed : $"{trimmed[..177]}...";
    }

    private static string[] ResolveTags(string text, string category, string priority)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            category.Replace(" ", "-").ToLowerInvariant(),
            priority.ToLowerInvariant()
        };

        if (ContainsAny(text, "urgent", "critical", "outage"))
        {
            tags.Add("urgent");
        }

        if (ContainsAny(text, "invoice", "payment", "refund"))
        {
            tags.Add("finance");
        }

        return tags.ToArray();
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(text.Contains);
}
