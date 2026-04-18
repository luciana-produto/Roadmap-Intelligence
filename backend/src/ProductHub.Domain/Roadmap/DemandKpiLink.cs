using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class DemandKpiLink : BaseEntity
{
    public Guid DemandId { get; private set; }
    public Guid KpiId { get; private set; }
    public ImpactType ImpactType { get; private set; }
    public decimal? EstimatedImpact { get; private set; }
    public ConfidenceLevel ConfidenceLevel { get; private set; }
    public string? Observation { get; private set; }

    private DemandKpiLink() { }

    internal static DemandKpiLink Create(
        Guid demandId,
        Guid kpiId,
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel,
        string? observation) =>
        new()
        {
            DemandId = demandId,
            KpiId = kpiId,
            ImpactType = impactType,
            EstimatedImpact = estimatedImpact,
            ConfidenceLevel = confidenceLevel,
            Observation = observation
        };

    public static DemandKpiLink FromRepository(
        Guid demandId,
        Guid kpiId,
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel,
        string? observation) =>
        Create(demandId, kpiId, impactType, estimatedImpact, confidenceLevel, observation);

    public void Update(
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel,
        string? observation)
    {
        ImpactType = impactType;
        EstimatedImpact = estimatedImpact;
        ConfidenceLevel = confidenceLevel;
        Observation = observation;
    }
}
