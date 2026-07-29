using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap;

public sealed class Kpi : AggregateRoot, IAuditableEntity
{
    public Guid? ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public KpiType Type { get; private set; }
    public KpiCategory Category { get; private set; }
    public KpiIndicator Indicator { get; private set; }
    public KpiOperation Operation { get; private set; }
    public IReadOnlyList<KpiUnit> AllowedUnits { get; private set; } = [];
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Kpi() { }

    public static Kpi Create(
        Guid? projectId,
        string name,
        KpiType type,
        KpiCategory category,
        KpiIndicator indicator,
        KpiOperation operation,
        IEnumerable<KpiUnit>? allowedUnits,
        string? description = null)
    {
        return new Kpi
        {
            ProjectId = projectId,
            Name = name,
            Type = type,
            Category = category,
            Indicator = indicator,
            Operation = operation,
            AllowedUnits = NormalizeUnits(allowedUnits),
            Description = description
        };
    }

    public void Update(
        string name,
        KpiType type,
        KpiCategory category,
        KpiIndicator indicator,
        KpiOperation operation,
        IEnumerable<KpiUnit>? allowedUnits,
        string? description = null)
    {
        Name = name;
        Type = type;
        Category = category;
        Indicator = indicator;
        Operation = operation;
        AllowedUnits = NormalizeUnits(allowedUnits);
        Description = description;
    }

    private static IReadOnlyList<KpiUnit> NormalizeUnits(IEnumerable<KpiUnit>? units) =>
        (units ?? []).Distinct().ToList();
}
