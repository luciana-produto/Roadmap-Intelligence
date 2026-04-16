using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class KpiMeasurementConfiguration : IEntityTypeConfiguration<KpiMeasurement>
{
    public void Configure(EntityTypeBuilder<KpiMeasurement> builder)
    {
        builder.ToTable("KpiMeasurements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MeasuredValue)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.MeasurementDate)
            .IsRequired();

        builder.Property(x => x.Result)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Observation)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.KpiId);
        builder.HasIndex(x => x.DemandId);

        builder.HasOne<Kpi>()
            .WithMany()
            .HasForeignKey(x => x.KpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RoadmapDemand>()
            .WithMany()
            .HasForeignKey(x => x.DemandId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
