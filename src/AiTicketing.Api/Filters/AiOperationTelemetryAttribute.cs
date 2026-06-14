using System.Diagnostics;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Common.Exceptions;
using AiTicketing.Infrastructure.Ai;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTicketing.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AiOperationTelemetryAttribute(string operationName) : Attribute, IAsyncActionFilter
{
    public string OperationName { get; } = operationName;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        ActionExecutedContext? executedContext = null;

        try
        {
            executedContext = await next();
        }
        finally
        {
            stopwatch.Stop();
            await RecordTelemetryAsync(context, executedContext, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task RecordTelemetryAsync(
        ActionExecutingContext context,
        ActionExecutedContext? executedContext,
        long durationMilliseconds)
    {
        try
        {
            var telemetry = context.HttpContext.RequestServices.GetService<IAiOperationTelemetry>();
            var telemetryContext = context.HttpContext.RequestServices.GetService<IAiOperationTelemetryContext>();
            var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<AiAssistantSettings>>().Value;
            if (telemetry is null || telemetryContext is null)
            {
                return;
            }

            var statusCode = ResolveStatusCode(executedContext, context.HttpContext.Response.StatusCode);
            var outcome = ResolveOutcome(executedContext, statusCode, telemetryContext);
            var providerCategory = telemetryContext.ProviderCategory ?? settings.EffectiveProvider;

            await telemetry.RecordAsync(new AiOperationTelemetryRecord(
                OperationName,
                providerCategory,
                outcome,
                durationMilliseconds,
                statusCode,
                telemetryContext.FallbackUsed,
                DateTimeOffset.UtcNow));
        }
        catch
        {
            // Telemetry must never affect the API operation.
        }
    }

    private static string ResolveOutcome(
        ActionExecutedContext? executedContext,
        int? statusCode,
        IAiOperationTelemetryContext telemetryContext)
    {
        if (executedContext?.Exception is OperationCanceledException)
        {
            return AiOperationOutcomes.Cancelled;
        }

        if (executedContext?.Exception is ValidationException || statusCode == StatusCodes.Status400BadRequest)
        {
            return AiOperationOutcomes.ValidationFailure;
        }

        if (executedContext?.Exception is AiProviderUnavailableException ||
            statusCode == StatusCodes.Status503ServiceUnavailable)
        {
            return AiOperationOutcomes.ProviderUnavailable;
        }

        return telemetryContext.FallbackUsed
            ? AiOperationOutcomes.FallbackSuccess
            : AiOperationOutcomes.Success;
    }

    private static int? ResolveStatusCode(ActionExecutedContext? executedContext, int currentStatusCode)
    {
        if (executedContext?.Exception is ValidationException)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (executedContext?.Exception is AiProviderUnavailableException)
        {
            return StatusCodes.Status503ServiceUnavailable;
        }

        if (executedContext?.Exception is OperationCanceledException)
        {
            return 499;
        }

        return executedContext?.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? currentStatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            ForbidResult => StatusCodes.Status403Forbidden,
            _ => currentStatusCode
        };
    }
}
