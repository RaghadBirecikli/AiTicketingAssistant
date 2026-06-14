using System.Net;
using AiTicketing.Application.Common.Exceptions;
using AiTicketing.Application.Common.Models;
using FluentValidation;

namespace AiTicketing.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (ex is ValidationException validationException)
            {
                await WriteValidationErrorAsync(context, validationException);
                return;
            }

            if (ex is AiProviderUnavailableException)
            {
                await WriteAiProviderUnavailableAsync(context);
                return;
            }

            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail("An unexpected error occurred.");
            await context.Response.WriteAsJsonAsync(response);
        }
    }

    private static async Task WriteValidationErrorAsync(HttpContext context, ValidationException exception)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/json";

        var errors = exception.Errors
            .Select(error => error.ErrorMessage)
            .ToArray();

        var response = ApiResponse<object>.Fail("Validation failed.", errors);
        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task WriteAiProviderUnavailableAsync(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Fail("AI provider is unavailable.");
        await context.Response.WriteAsJsonAsync(response);
    }
}
