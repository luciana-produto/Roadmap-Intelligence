using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using ValidationException = ProductHub.Application.Common.Exceptions.ValidationException;
using ValidationFailure = ProductHub.Application.Common.Exceptions.ValidationFailure;

namespace ProductHub.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => r.Errors.Count > 0)
            .SelectMany(r => r.Errors)
            .Select(f => new ValidationFailure(f.PropertyName, f.ErrorMessage))
            .ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning("Validation failed for {RequestName} with {FailureCount} failure(s).",
                typeof(TRequest).Name, failures.Count);

            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
