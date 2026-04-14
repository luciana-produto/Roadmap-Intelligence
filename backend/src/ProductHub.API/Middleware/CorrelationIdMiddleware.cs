using Serilog.Context;
using ProductHub.Shared.Constants;

namespace ProductHub.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[AppConstants.Http.CorrelationIdHeader] = correlationId;
        context.Response.Headers[AppConstants.Http.CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty(AppConstants.Logging.CorrelationIdProperty, correlationId))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(AppConstants.Http.CorrelationIdHeader, out var value)
            && !string.IsNullOrWhiteSpace(value))
            return value!;

        return Guid.NewGuid().ToString("N");
    }
}
