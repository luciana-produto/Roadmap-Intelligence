using System.Net;
using System.Text.Json;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Shared.Constants;
using ValidationException = ProductHub.Application.Common.Exceptions.ValidationException;

namespace ProductHub.API.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = ResolveCorrelationId(context);
            logger.LogError(ex, "Unhandled exception [{CorrelationId}]: {Message}", correlationId, ex.Message);
            await WriteErrorResponseAsync(context, ex, correlationId);
        }
    }

    private static string ResolveCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(AppConstants.Http.CorrelationIdHeader, out var value)
            ? value?.ToString() ?? Guid.NewGuid().ToString("N")
            : Guid.NewGuid().ToString("N");

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception, string correlationId)
    {
        var (statusCode, title, detail, errors) = MapException(exception);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse(title, detail, (int)statusCode, correlationId, errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    private static (HttpStatusCode Status, string Title, string Detail, IDictionary<string, string[]>? Errors) MapException(
        Exception exception) => exception switch
    {
        ValidationException ve =>
            (HttpStatusCode.UnprocessableEntity,
             "Erro de Validação",
             AppConstants.UserMessages.ValidationError,
             ve.Errors),

        NotFoundException =>
            (HttpStatusCode.NotFound,
             "Recurso Não Encontrado",
             AppConstants.UserMessages.NotFound,
             null),

        UnauthorizedAccessException =>
            (HttpStatusCode.Unauthorized,
             "Acesso Negado",
             AppConstants.UserMessages.Unauthorized,
             null),

        _ =>
            (HttpStatusCode.InternalServerError,
             "Erro Interno",
             AppConstants.UserMessages.GenericError,
             null)
    };
}

internal sealed record ErrorResponse(
    string Title,
    string Detail,
    int Status,
    string CorrelationId,
    IDictionary<string, string[]>? Errors = null);
