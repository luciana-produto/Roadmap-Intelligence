using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class KpiConfiguration : IEntityTypeConfiguration<Kpi>
{
    public void Configure(EntityTypeBuilder<Kpi> builder)
    {
        builder.ToTable("Kpis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Lever)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Calculation)
            .HasMaxLength(500);

        builder.Property(x => x.Target)
            .HasPrecision(18, 4);

        builder.Property(x => x.CurrentValue)
            .HasPrecision(18, 4);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
    }
}
