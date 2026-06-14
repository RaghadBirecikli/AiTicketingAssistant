using AiTicketing.Application.Ai;
using Microsoft.Extensions.Logging;

namespace AiTicketing.Infrastructure.Ai;

public sealed class LoggingAiOperationTelemetry(
    ILogger<LoggingAiOperationTelemetry> logger) : IAiOperationTelemetry
{
    public Task RecordAsync(AiOperationTelemetryRecord record, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "AI operation completed: Operation={OperationName} Provider={ProviderCategory} Outcome={Outcome} DurationMs={DurationMilliseconds} StatusCode={StatusCode} FallbackUsed={FallbackUsed}",
            record.OperationName,
            record.ProviderCategory,
            record.Outcome,
            record.DurationMilliseconds,
            record.StatusCode,
            record.FallbackUsed);

        return Task.CompletedTask;
    }
}
