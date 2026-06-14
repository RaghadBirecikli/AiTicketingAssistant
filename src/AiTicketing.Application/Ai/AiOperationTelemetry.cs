namespace AiTicketing.Application.Ai;

public static class AiOperationNames
{
    public const string SuggestReply = "suggest-reply";
    public const string Summarize = "summarize";
    public const string SuggestTriage = "suggest-triage";
}

public static class AiRateLimitPolicyNames
{
    public const string AiEndpoints = "ai-ticket-operations";
}

public static class AiOperationOutcomes
{
    public const string Success = "success";
    public const string FallbackSuccess = "fallback-success";
    public const string ProviderUnavailable = "provider-unavailable";
    public const string ValidationFailure = "validation-failure";
    public const string Cancelled = "cancelled";
    public const string RateLimited = "rate-limited";
}

public sealed record AiOperationTelemetryRecord(
    string OperationName,
    string ProviderCategory,
    string Outcome,
    long DurationMilliseconds,
    int? StatusCode,
    bool FallbackUsed,
    DateTimeOffset TimestampUtc);

public interface IAiOperationTelemetry
{
    Task RecordAsync(AiOperationTelemetryRecord record, CancellationToken cancellationToken = default);
}

public interface IAiOperationTelemetryContext
{
    string? ProviderCategory { get; }

    bool FallbackUsed { get; }

    void MarkProvider(string providerCategory);

    void MarkFallback(string providerCategory);
}
