using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap;

public sealed class Kpi : AggregateRoot, IAuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public KpiType Type { get; private set; }
    public KpiLever Lever { get; private set; }
    public string? Description { get; private set; }
    public string? Calculation { get; private set; }
    public decimal? Target { get; private set; }
    public decimal? CurrentValue { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Kpi() { }

    public static Kpi Create(
        Guid projectId,
        string name,
        KpiType type,
        KpiLever lever,
        string? description = null,
        string? calculation = null,
        decimal? target = null,
        decimal? currentValue = null)
    {
        return new Kpi
        {
            ProjectId = projectId,
            Name = name,
            Type = type,
            Lever = lever,
            Description = description,
            Calculation = calculation,
            Target = target,
            CurrentValue = currentValue
        };
    }

    public void Update(
        string name,
        KpiType type,
        KpiLever lever,
        string? description = null,
        string? calculation = null,
        decimal? target = null,
        decimal? currentValue = null)
    {
        Name = name;
        Type = type;
        Lever = lever;
        Description = description;
        Calculation = calculation;
        Target = target;
        CurrentValue = currentValue;
    }
}
