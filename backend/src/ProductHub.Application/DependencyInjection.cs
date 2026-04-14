using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ProductHub.Application.Common.Behaviors;

namespace ProductHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(SlowRequestBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
