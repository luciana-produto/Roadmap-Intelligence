using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapCapacityConfiguration : IEntityTypeConfiguration<RoadmapCapacity>
{
    public void Configure(EntityTypeBuilder<RoadmapCapacity> builder)
    {
        builder.ToTable("RoadmapCapacities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProjectId)
            .IsRequired();

        builder.Property(x => x.QuarterYear)
            .IsRequired();

        builder.Property(x => x.QuarterNumber)
            .IsRequired();

        builder.Property(x => x.CapacityHours)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.Observation)
            .HasMaxLength(2000);

        builder.Ignore(x => x.Quarter);

        builder.HasIndex(x => new { x.ProjectId, x.QuarterYear, x.QuarterNumber })
            .IsUnique();
    }
}