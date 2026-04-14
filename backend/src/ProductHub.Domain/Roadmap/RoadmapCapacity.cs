using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapCapacity : AggregateRoot
{
    public Guid ProjectId { get; private set; }
    public int QuarterYear { get; private set; }
    public int QuarterNumber { get; private set; }
    public decimal CapacityHours { get; private set; }
    public string? Observation { get; private set; }

    public Quarter Quarter => Quarter.Create(QuarterYear, QuarterNumber);

    private RoadmapCapacity() { }

    public static RoadmapCapacity Create(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        decimal capacityHours,
        string? observation = null)
    {
        Quarter.Create(quarterYear, quarterNumber);

        if (capacityHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityHours), "Capacity hours must be greater than zero.");

        return new RoadmapCapacity
        {
            ProjectId = projectId,
            QuarterYear = quarterYear,
            QuarterNumber = quarterNumber,
            CapacityHours = capacityHours,
            Observation = NormalizeObservation(observation)
        };
    }

    public void Update(decimal capacityHours, string? observation = null)
    {
        if (capacityHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityHours), "Capacity hours must be greater than zero.");

        CapacityHours = capacityHours;
        Observation = NormalizeObservation(observation);
    }

    private static string? NormalizeObservation(string? observation)
    {
        var normalized = observation?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}