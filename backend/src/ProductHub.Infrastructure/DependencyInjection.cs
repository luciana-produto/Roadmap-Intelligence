using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("SqlServer");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
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

        return services;
    }
}
