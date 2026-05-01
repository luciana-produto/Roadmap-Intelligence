using Serilog;
using Serilog.Enrichers.CorrelationId;
using Microsoft.EntityFrameworkCore;
using ProductHub.API.Middleware;
using ProductHub.Application;
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
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    builder.Services.AddCors(options =>
        options.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins(
                      builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:3000"])
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders("X-Correlation-ID")));

    var sqlConnectionString = builder.Configuration.GetConnectionString("SqlServer");
    var healthChecks = builder.Services.AddHealthChecks();
    if (!string.IsNullOrWhiteSpace(sqlConnectionString))
        healthChecks.AddSqlServer(sqlConnectionString, name: "sqlserver", tags: ["db", "ready"]);

    var app = builder.Build();

    await InitializeDatabaseAsync(app.Services, !string.IsNullOrWhiteSpace(sqlConnectionString));

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseSerilogRequestLogging(opts =>
        opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        });

    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

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

static async Task InitializeDatabaseAsync(IServiceProvider services, bool hasPersistentDatabase)
{
    var attempts = hasPersistentDatabase ? AppConstants.Database.RetryCount : 1;

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
                await ApplySchemaUpdatesAsync(db, hasPersistentDatabase);
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
    await finalDb.Database.EnsureCreatedAsync();
        await ApplySchemaUpdatesAsync(finalDb, hasPersistentDatabase);

    static async Task ApplySchemaUpdatesAsync(AppDbContext db, bool hasPersistentDatabase)
    {
        if (!hasPersistentDatabase)
            return;

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('RoadmapDemands', 'IssueLinksJson') IS NULL
            BEGIN
                ALTER TABLE RoadmapDemands ADD IssueLinksJson NVARCHAR(MAX) NULL;
            END
            """);
    }
    await RoadmapSeeder.SeedAsync(finalDb);
}
