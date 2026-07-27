using Serilog;
using Serilog.Enrichers.CorrelationId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using ProductHub.API.Middleware;
using ProductHub.API.Security;
using ProductHub.Application;
using ProductHub.Application.Access;
using ProductHub.Application.Common;
using ProductHub.Infrastructure;
using ProductHub.Infrastructure.Persistence;
using ProductHub.Infrastructure.Persistence.Seed;
using ProductHub.Infrastructure.Security;
using ProductHub.Shared.Constants;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddKeyVaultIfConfigured(builder.Environment);

    builder.Host.UseSerilog((context, services, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext()
              .Enrich.WithCorrelationId()
              .Enrich.WithMachineName()
              .Enrich.WithEnvironmentName()
              .WriteTo.Console(
                  outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
              .WriteTo.Seq(context.Configuration["Serilog:SeqUrl"] ?? "http://seq:5341"));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    // Liga as permissões de acesso (super-admins) à seção "Authorization" da configuração.
    builder.Services.Configure<AccessOptions>(builder.Configuration.GetSection(AccessOptions.SectionName));

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();

    // Usuário atual (para auditoria de quem excluiu itens, etc.).
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services
        .AddAuthentication(SsoAuthenticationHandler.SchemeName)
        .AddScheme<SsoAuthenticationOptions, SsoAuthenticationHandler>(
            SsoAuthenticationHandler.SchemeName,
            options =>
            {
                options.SsoBaseUrl = builder.Configuration["Sso:BaseUrl"] ?? string.Empty;
                options.TenantKey = builder.Configuration["Sso:TenantKey"] ?? string.Empty;
                // Aceita o "Dev Login (local only)" sem chamar o SSO.
                // Por padrão só em Development; pode ser ligado explicitamente via Sso:AllowDevSession
                // para rodar localmente em containers (NUNCA habilitar em ambiente exposto/publicado).
                options.AllowDevSession =
                    builder.Configuration.GetValue<bool?>("Sso:AllowDevSession")
                    ?? builder.Environment.IsDevelopment();
            });

    // Por padrão, todo endpoint exige um usuário autenticado.
    // Endpoints públicos (health, OpenAPI) usam .AllowAnonymous() explicitamente.
    // Policies adicionais exigem permissões específicas (roadmap edição / cadastros / gerir acessos).
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy(AccessPolicies.RoadmapEdit, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(AccessPolicies.PermissionRoadmapEdit));
        });

        options.AddPolicy(AccessPolicies.Registrations, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(AccessPolicies.PermissionRegistrations));
        });

        options.AddPolicy(AccessPolicies.ManageAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(AccessPolicies.PermissionManageAccess));
        });
    });

    builder.Services.AddCors(options =>
        options.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins(
                      builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:3000"])
                  .WithMethods("GET", "POST", "PUT", "DELETE")
                  .WithHeaders("Authorization", "Content-Type", AppConstants.Http.CorrelationIdHeader)
                  .WithExposedHeaders(AppConstants.Http.CorrelationIdHeader)));

    var sqlConnectionString = builder.Configuration.GetConnectionString("SqlServer");
    var seedMockData = builder.Configuration.GetValue<bool?>("Roadmap:SeedMockData") ?? builder.Environment.IsDevelopment();
    var healthChecks = builder.Services.AddHealthChecks();
    if (!string.IsNullOrWhiteSpace(sqlConnectionString))
        healthChecks.AddSqlServer(sqlConnectionString, name: "sqlserver", tags: ["db", "ready"]);

    var app = builder.Build();

    await InitializeDatabaseAsync(app.Services, !string.IsNullOrWhiteSpace(sqlConnectionString), seedMockData);

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi().AllowAnonymous();

    app.UseSerilogRequestLogging(opts =>
        opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        });

    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool hasPersistentDatabase, bool seedMockData)
{
    var attempts = hasPersistentDatabase ? AppConstants.Database.RetryCount : 1;

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await EnsureDatabaseReadyAsync(db, hasPersistentDatabase);
            if (seedMockData)
                await RoadmapSeeder.SeedAsync(db);
            return;
        }
        catch (Exception ex) when (attempt < attempts)
        {
            var delay = TimeSpan.FromSeconds(AppConstants.Database.RetryBaseDelaySeconds * attempt);
            Log.Warning(ex,
                "Database initialization failed on attempt {Attempt}/{MaxAttempts}. Retrying in {DelaySeconds}s.",
                attempt,
                attempts,
                delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }

    using var finalScope = services.CreateScope();
    var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await EnsureDatabaseReadyAsync(finalDb, hasPersistentDatabase);
    if (seedMockData)
        await RoadmapSeeder.SeedAsync(finalDb);
}

static async Task EnsureDatabaseReadyAsync(AppDbContext db, bool hasPersistentDatabase)
{
    if (!hasPersistentDatabase)
    {
        await db.Database.EnsureCreatedAsync();
        return;
    }

    var migrations = db.Database.GetMigrations();
    if (migrations.Any())
    {
        await db.Database.MigrateAsync();
        return;
    }

    var schemaScriptPath = Path.Combine(AppContext.BaseDirectory, "sql", "producthub-schema.sql");
    if (!File.Exists(schemaScriptPath))
    {
        throw new FileNotFoundException(
            $"Persistent database initialization requires the bundled schema script when no EF migrations are present. Missing file: {schemaScriptPath}");
    }

    var schemaScript = await File.ReadAllTextAsync(schemaScriptPath);
    if (string.IsNullOrWhiteSpace(schemaScript))
    {
        throw new InvalidOperationException($"Schema script is empty: {schemaScriptPath}");
    }

    await db.Database.ExecuteSqlRawAsync(schemaScript);
}
