using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapCapacity : AggregateRoot
{
    public Guid ProjectId { get; private set; }
    public int QuarterYear { get; private set; }
    public int QuarterNumber { get; private set; }
    public decimal CapacityHours { get; private set; }
    // Percentual do capacity reservado a Débito Técnico (0–100). Default 20.
    public decimal TechnicalDebtPercent { get; private set; } = 20m;
    public string? Observation { get; private set; }

    public Quarter Quarter => Quarter.Create(QuarterYear, QuarterNumber);

    private RoadmapCapacity() { }

    public static RoadmapCapacity Create(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        decimal capacityHours,
        string? observation = null,
        decimal technicalDebtPercent = 20m)
    {
        Quarter.Create(quarterYear, quarterNumber);

        if (capacityHours < 0)
            throw new ArgumentOutOfRangeException(nameof(capacityHours), "Capacity hours cannot be negative.");

        return new RoadmapCapacity
        {
            ProjectId = projectId,
            QuarterYear = quarterYear,
            QuarterNumber = quarterNumber,
            CapacityHours = capacityHours,
            TechnicalDebtPercent = NormalizePercent(technicalDebtPercent),
            Observation = NormalizeObservation(observation)
        };
    }

    public void Update(decimal capacityHours, string? observation = null, decimal technicalDebtPercent = 20m)
    {
        if (capacityHours < 0)
            throw new ArgumentOutOfRangeException(nameof(capacityHours), "Capacity hours cannot be negative.");

        CapacityHours = capacityHours;
        TechnicalDebtPercent = NormalizePercent(technicalDebtPercent);
        Observation = NormalizeObservation(observation);
    }

    private static decimal NormalizePercent(decimal percent) =>
        percent < 0m ? 0m : percent > 100m ? 100m : percent;

    private static string? NormalizeObservation(string? observation)
    {
        var normalized = observation?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}