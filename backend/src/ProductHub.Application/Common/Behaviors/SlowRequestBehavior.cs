using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ProductHub.Shared.Constants;

namespace ProductHub.Application.Common.Behaviors;

public sealed class SlowRequestBehavior<TRequest, TResponse>(
    ILogger<SlowRequestBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly TimeSpan Threshold =
        TimeSpan.FromSeconds(AppConstants.Http.SlowRequestThresholdSeconds);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        stopwatch.Stop();

        if (stopwatch.Elapsed > Threshold)
            LogSlowRequest(request, stopwatch.Elapsed);

        return response;
    }

    private void LogSlowRequest(TRequest request, TimeSpan elapsed) =>
        logger.LogWarning(
            "SLOW REQUEST — {RequestName} took {ElapsedSeconds:F1}s (threshold: {ThresholdSeconds}s). {@Request}",
            typeof(TRequest).Name,
            elapsed.TotalSeconds,
            Threshold.TotalSeconds,
            request);
}
