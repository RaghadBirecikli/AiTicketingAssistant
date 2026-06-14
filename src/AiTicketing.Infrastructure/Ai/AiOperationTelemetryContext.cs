using AiTicketing.Application.Ai;

namespace AiTicketing.Infrastructure.Ai;

public sealed class AiOperationTelemetryContext : IAiOperationTelemetryContext
{
    public string? ProviderCategory { get; private set; }

    public bool FallbackUsed { get; private set; }

    public void MarkProvider(string providerCategory)
    {
        ProviderCategory = providerCategory;
        FallbackUsed = false;
    }

    public void MarkFallback(string providerCategory)
    {
        ProviderCategory = providerCategory;
        FallbackUsed = true;
    }
}
