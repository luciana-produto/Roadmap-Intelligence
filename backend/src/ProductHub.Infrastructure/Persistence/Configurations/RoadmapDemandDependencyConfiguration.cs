using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapDemandDependencyConfiguration : IEntityTypeConfiguration<RoadmapDemandDependency>
{
    public void Configure(EntityTypeBuilder<RoadmapDemandDependency> builder)
    {
        builder.ToTable("RoadmapDemandDependencies");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.DemandId, x.DependsOnDemandId }).IsUnique();

        builder.HasOne<RoadmapDemand>()
            .WithMany()
            .HasForeignKey(x => x.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RoadmapDemand>()
            .WithMany()
            .HasForeignKey(x => x.DependsOnDemandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}