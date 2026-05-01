using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Infrastructure.Persistence.Interceptors;

namespace ProductHub.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    AuditSaveChangesInterceptor auditInterceptor)
    : DbContext(options), IUnitOfWork
{
    public DbSet<RoadmapProject> RoadmapProjects => Set<RoadmapProject>();
    public DbSet<RoadmapProduct> RoadmapProducts => Set<RoadmapProduct>();
    public DbSet<RoadmapDemand> RoadmapDemands => Set<RoadmapDemand>();
    public DbSet<RoadmapDemandProduct> RoadmapDemandProducts => Set<RoadmapDemandProduct>();
    public DbSet<RoadmapDemandProject> RoadmapDemandProjects => Set<RoadmapDemandProject>();
    public DbSet<RoadmapDemandDependency> RoadmapDemandDependencies => Set<RoadmapDemandDependency>();
    public DbSet<RoadmapCapacity> RoadmapCapacities => Set<RoadmapCapacity>();
    public DbSet<Kpi> Kpis => Set<Kpi>();
    public DbSet<DemandKpiLink> DemandKpiLinks => Set<DemandKpiLink>();
    public DbSet<KpiMeasurement> KpiMeasurements => Set<KpiMeasurement>();
    public DbSet<DemandTradeOff> DemandTradeOffs => Set<DemandTradeOff>();
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
