using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class DemandKpiLink : BaseEntity
{
    public Guid DemandId { get; private set; }
    public Guid KpiId { get; private set; }
    public ImpactType ImpactType { get; private set; }
    public decimal? EstimatedImpact { get; private set; }
    public ConfidenceLevel ConfidenceLevel { get; private set; }

    private DemandKpiLink() { }

    internal static DemandKpiLink Create(
        Guid demandId,
        Guid kpiId,
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel) =>
        new()
        {
            DemandId = demandId,
            KpiId = kpiId,
            ImpactType = impactType,
            EstimatedImpact = estimatedImpact,
            ConfidenceLevel = confidenceLevel
        };

    public static DemandKpiLink FromRepository(
        Guid demandId,
        Guid kpiId,
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel) =>
        Create(demandId, kpiId, impactType, estimatedImpact, confidenceLevel);

    public void Update(
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel)
    {
        ImpactType = impactType;
        EstimatedImpact = estimatedImpact;
        ConfidenceLevel = confidenceLevel;
    }
}
