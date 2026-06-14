namespace AiTicketing.Application.Common.Models;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message = null,
    IReadOnlyCollection<string>? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);

    public static ApiResponse<T> Fail(string message, IReadOnlyCollection<string>? errors = null) =>
        new(false, default, message, errors);
}
