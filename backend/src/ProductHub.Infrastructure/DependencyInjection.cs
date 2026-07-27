using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductHub.Application.Access;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;
using ProductHub.Infrastructure.Access;
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
        // Scoped porque agora captura o usuário atual (ICurrentUserService, scoped) para auditoria.
        services.AddScoped<AuditSaveChangesInterceptor>();

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

            // O interceptor de auditoria é adicionado via AppDbContext.OnConfiguring
            // (injetado no construtor), evitando resolver um serviço scoped pelo provider raiz.
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IRoadmapProjectRepository, RoadmapProjectRepository>();
        services.AddScoped<IRoadmapDemandRepository, RoadmapDemandRepository>();
        services.AddScoped<IRoadmapCapacityRepository, RoadmapCapacityRepository>();
        services.AddScoped<IKpiRepository, KpiRepository>();

        // Permissões de acesso (Opção B: cadastro por e-mail + super-admins em config).
        // A ligação das AccessOptions à configuração é feita no Program.cs (projeto Web,
        // que já traz o binder de configuração).
        services.AddScoped<IUserAccessService, UserAccessService>();

        return services;
    }
}
