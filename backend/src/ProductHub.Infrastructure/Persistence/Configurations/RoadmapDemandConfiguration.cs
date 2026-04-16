using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProductHub.Domain.Roadmap;
using System.Text.Json;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapDemandConfiguration : IEntityTypeConfiguration<RoadmapDemand>
{
    public void Configure(EntityTypeBuilder<RoadmapDemand> builder)
    {
        builder.ToTable("RoadmapDemands");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Classification)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Observation).HasMaxLength(2000);
        builder.Property(x => x.JiraIssue).HasMaxLength(100);
        builder.Property(x => x.Hours);

        var customersConverter = new ValueConverter<IReadOnlyList<string>, string?>(
            customers => JsonSerializer.Serialize(customers ?? Array.Empty<string>(), (JsonSerializerOptions?)null),
            value => ParseCustomers(value));

        var customersComparer = new ValueComparer<IReadOnlyList<string>>(
            (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
            customers => customers == null
                ? 0
                : customers.Aggregate(0, (hash, customer) => HashCode.Combine(hash, customer.GetHashCode())),
            customers => customers == null
                ? new List<string>()
                : customers.ToList());

        var customersProperty = builder.Property(x => x.Customers);
        customersProperty.HasConversion(customersConverter);
        customersProperty.IsRequired(false);
        customersProperty.Metadata.SetValueComparer(customersComparer);

        builder.Property(x => x.IsBlocked).IsRequired();
        builder.Property(x => x.BlockedReason).HasMaxLength(500);
        builder.Property(x => x.DeliveryDate);

        builder.Property(x => x.ProblemClarity);
        builder.Property(x => x.HasNoKpi).IsRequired();

        builder.Ignore(x => x.Quarter);

        builder.Navigation(x => x.Products)
            .HasField("_products")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(x => x.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProjectId, x.QuarterYear, x.QuarterNumber, x.SortOrder });
    }

    private static IReadOnlyList<string> ParseCustomers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        if (value.TrimStart().StartsWith("["))
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
