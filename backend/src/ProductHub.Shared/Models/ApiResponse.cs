namespace ProductHub.Shared.Models;

public sealed record ApiResponse<T>
{
    public T? Data { get; init; }
    public bool Success { get; init; }
    public string? CorrelationId { get; init; }
    public string? Message { get; init; }
    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? correlationId = null) =>
        new() { Data = data, Success = true, CorrelationId = correlationId };

    public static ApiResponse<T> Fail(string message, string? correlationId = null, IDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, CorrelationId = correlationId, Errors = errors };
}

public sealed record ApiResponse
{
    public bool Success { get; init; }
    public string? CorrelationId { get; init; }
    public string? Message { get; init; }
    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse Ok(string? correlationId = null) =>
        new() { Success = true, CorrelationId = correlationId };

    public static ApiResponse Fail(string message, string? correlationId = null, IDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, CorrelationId = correlationId, Errors = errors };
}
