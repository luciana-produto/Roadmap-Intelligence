using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class DemandTradeOffConfiguration : IEntityTypeConfiguration<DemandTradeOff>
{
    public void Configure(EntityTypeBuilder<DemandTradeOff> builder)
    {
        builder.ToTable("DemandTradeOffs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProjectId)
            .IsRequired();

        builder.Property(x => x.QuarterYear)
            .IsRequired();

        builder.Property(x => x.QuarterNumber)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Observation)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.DeprioritizedDemandId);
        builder.HasIndex(x => new { x.ProjectId, x.QuarterYear, x.QuarterNumber });

        builder.HasOne<RoadmapDemand>()
            .WithMany()
            .HasForeignKey(x => x.DeprioritizedDemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RoadmapDemand>()
            .WithMany()
            .HasForeignKey(x => x.ReplacementDemandId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
