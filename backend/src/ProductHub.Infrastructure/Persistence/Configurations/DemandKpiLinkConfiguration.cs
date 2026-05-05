using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class DemandKpiLinkConfiguration : IEntityTypeConfiguration<DemandKpiLink>
{
    public void Configure(EntityTypeBuilder<DemandKpiLink> builder)
    {
        builder.ToTable("DemandKpiLinks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImpactType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.ConfidenceLevel)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.EstimatedImpact)
            .HasPrecision(18, 4);

        builder.Property(x => x.Observation)
            .HasMaxLength(1000);

        builder.Property(x => x.MeasurementReferenceUrl)
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.DemandId, x.KpiId }).IsUnique();

        builder.HasOne<RoadmapDemand>()
            .WithMany()
            .HasForeignKey(x => x.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Kpi>()
            .WithMany()
            .HasForeignKey(x => x.KpiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
