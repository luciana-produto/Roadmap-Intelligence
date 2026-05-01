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

        builder.Property(x => x.ItemType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.ParentDemandId)
            .IsRequired(false);

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
        builder.Property(x => x.DeprioritizationReason)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired(false);
        builder.Property(x => x.ReplacementDemandId)
            .IsRequired(false);
        builder.Property(x => x.JiraIssue).HasMaxLength(100);
        var issueLinksConverter = new ValueConverter<IReadOnlyList<RoadmapIssueLink>, string?>(
            links => JsonSerializer.Serialize(links ?? Array.Empty<RoadmapIssueLink>(), (JsonSerializerOptions?)null),
            value => ParseIssueLinks(value));

        var issueLinksComparer = new ValueComparer<IReadOnlyList<RoadmapIssueLink>>(
            (left, right) => ReferenceEquals(left, right)
                || (left != null && right != null && left.Count == right.Count && left.Zip(right).All(pair => pair.First.Key == pair.Second.Key && pair.First.Url == pair.Second.Url)),
            links => links == null
                ? 0
                : links.Aggregate(0, (hash, link) => HashCode.Combine(hash, link.Key.GetHashCode(), link.Url.GetHashCode())),
            links => links == null
                ? Array.Empty<RoadmapIssueLink>()
                : links.Select(link => RoadmapIssueLink.Create(link.Key, link.Url)).ToList());

        var issueLinksProperty = builder.Property(x => x.IssueLinks)
            .HasColumnName("IssueLinksJson");
        issueLinksProperty.Metadata.SetField("_issueLinks");
        issueLinksProperty.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);
        issueLinksProperty.HasConversion(issueLinksConverter);
        issueLinksProperty.IsRequired(false);
        issueLinksProperty.Metadata.SetValueComparer(issueLinksComparer);
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
        builder.Property(x => x.PromisedDate);
        builder.Property(x => x.DeliveryDate);

        builder.Property(x => x.ProblemClarity);
        builder.Property(x => x.HasNoKpi).IsRequired();
        builder.Property(x => x.NoKpiClassification)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Ignore(x => x.Quarter);

        builder.Navigation(x => x.Products)
            .HasField("_products")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(x => x.ProjectLinks)
            .HasField("_projectLinks")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(x => x.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ProjectLinks)
            .WithOne()
            .HasForeignKey(x => x.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProjectId, x.QuarterYear, x.QuarterNumber, x.SortOrder });
        builder.HasIndex(x => x.ParentDemandId);
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

    private static IReadOnlyList<RoadmapIssueLink> ParseIssueLinks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<RoadmapIssueLink>>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
