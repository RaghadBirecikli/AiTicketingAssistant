namespace AiTicketing.Infrastructure.Ai;

public sealed class AiAssistantSettings
{
    public const string SectionName = "AiAssistant";

    public string? Provider { get; init; } = AiAssistantProviders.RuleBased;

    public OllamaSettings Ollama { get; init; } = new();

    public AiRateLimitSettings RateLimit { get; init; } = new();

    public string EffectiveProvider =>
        string.IsNullOrWhiteSpace(Provider)
            ? AiAssistantProviders.RuleBased
            : Provider.Trim();

    public bool IsKnownProvider() =>
        string.Equals(EffectiveProvider, AiAssistantProviders.RuleBased, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(EffectiveProvider, AiAssistantProviders.Ollama, StringComparison.OrdinalIgnoreCase);
}

public sealed class AiRateLimitSettings
{
    public int PermitLimit { get; init; } = 10;

    public int WindowSeconds { get; init; } = 60;
}
