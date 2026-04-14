using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Infrastructure.Persistence.Configurations;

namespace ProductHub.Infrastructure.Tests.Configurations;

public sealed class RoadmapDemandConfigurationTests
{
    [Fact]
    public async Task SaveChanges_WhenCustomersIsEmpty_ShouldPersistAndReloadAsEmptyList()
    {
        var options = new DbContextOptionsBuilder<RoadmapConfigurationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var demandId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await using (var context = new RoadmapConfigurationDbContext(options))
        {
            var demand = RoadmapDemand.Create(
                title: "Demanda",
                description: null,
                projectId: Guid.NewGuid(),
                quarterYear: 2026,
                quarterNumber: 1,
                type: DemandType.Planned,
                classification: DemandClassification.Evolution,
                productIds: [productId],
                customers: []);

            typeof(ProductHub.Domain.Common.BaseEntity)
                .GetProperty(nameof(ProductHub.Domain.Common.BaseEntity.Id))!
                .SetValue(demand, demandId);

            context.RoadmapDemands.Add(demand);
            await context.SaveChangesAsync();
        }

        await using (var context = new RoadmapConfigurationDbContext(options))
        {
            var loaded = await context.RoadmapDemands.AsNoTracking().SingleAsync(d => d.Id == demandId);
            loaded.Customers.Should().NotBeNull();
            loaded.Customers.Should().BeEmpty();
        }
    }

    [Fact]
    public void Model_WhenCustomersConfigured_ShouldAllowNullProviderValues()
    {
        var options = new DbContextOptionsBuilder<RoadmapConfigurationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new RoadmapConfigurationDbContext(options);

        var property = context.Model
            .FindEntityType(typeof(RoadmapDemand))!
            .FindProperty(nameof(RoadmapDemand.Customers));

        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    private sealed class RoadmapConfigurationDbContext(DbContextOptions<RoadmapConfigurationDbContext> options) : DbContext(options)
    {
        public DbSet<RoadmapDemand> RoadmapDemands => Set<RoadmapDemand>();
        public DbSet<RoadmapDemandProduct> RoadmapDemandProducts => Set<RoadmapDemandProduct>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new RoadmapDemandConfiguration());
            modelBuilder.Entity<RoadmapDemandProduct>().HasKey(x => new { x.DemandId, x.ProductId });
        }
    }
}