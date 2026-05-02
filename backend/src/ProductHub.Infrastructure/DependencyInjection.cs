using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;
using ProductHub.Infrastructure.Persistence;
using ProductHub.Infrastructure.Persistence.Interceptors;
using ProductHub.Infrastructure.Repositories;
using ProductHub.Shared.Constants;

namespace ProductHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("SqlServer");
        var useInMemoryFallback = environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString);

        if (!useInMemoryFallback && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SqlServer must be configured when the application is not running in Development.");
        }

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (useInMemoryFallback)
                options.UseInMemoryDatabase("ProductHub");
            else
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: AppConstants.Database.RetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(AppConstants.Database.RetryBaseDelaySeconds),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(AppConstants.Database.CommandTimeoutSeconds);
                });

            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IRoadmapProjectRepository, RoadmapProjectRepository>();
        services.AddScoped<IRoadmapDemandRepository, RoadmapDemandRepository>();
        services.AddScoped<IRoadmapCapacityRepository, RoadmapCapacityRepository>();
        services.AddScoped<IKpiRepository, KpiRepository>();

        return services;
    }
}
