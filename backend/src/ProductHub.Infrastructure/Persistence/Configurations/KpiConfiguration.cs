using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        builder.Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Indicator)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Operation)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Unidades permitidas (lista de enums) armazenadas como CSV dos nomes.
        var unitsConverter = new ValueConverter<IReadOnlyList<KpiUnit>, string>(
            units => string.Join(',', (units ?? Array.Empty<KpiUnit>()).Select(u => u.ToString())),
            value => ParseUnits(value));

        var unitsComparer = new ValueComparer<IReadOnlyList<KpiUnit>>(
            (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
            list => list.Aggregate(0, (hash, unit) => HashCode.Combine(hash, unit.GetHashCode())),
            list => list.ToList());

        var allowedUnitsProperty = builder.Property(x => x.AllowedUnits);
        allowedUnitsProperty.HasConversion(unitsConverter);
        allowedUnitsProperty.HasColumnName("AllowedUnits");
        allowedUnitsProperty.HasMaxLength(200);
        allowedUnitsProperty.IsRequired();
        allowedUnitsProperty.Metadata.SetValueComparer(unitsComparer);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.ProjectId)
            .IsRequired(false);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
    }

    private static IReadOnlyList<KpiUnit> ParseUnits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var result = new List<KpiUnit>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<KpiUnit>(part, true, out var unit))
                result.Add(unit);
        }
        return result;
    }
}
