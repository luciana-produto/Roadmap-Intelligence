using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapDemandProjectConfiguration : IEntityTypeConfiguration<RoadmapDemandProject>
{
    public void Configure(EntityTypeBuilder<RoadmapDemandProject> builder)
    {
        builder.ToTable("RoadmapDemandProjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DemandId)
            .IsRequired();

        builder.Property(x => x.ProjectId)
            .IsRequired();

        builder.HasIndex(x => new { x.DemandId, x.ProjectId })
            .IsUnique();
    }
}