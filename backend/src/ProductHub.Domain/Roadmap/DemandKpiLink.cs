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
    public string? MeasurementReferenceUrl { get; private set; }

    private DemandKpiLink() { }

    internal static DemandKpiLink Create(
        Guid demandId,
        Guid kpiId,
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel,
        string? observation,
        string? measurementReferenceUrl = null) =>
        new()
        {
            DemandId = demandId,
            KpiId = kpiId,
            ImpactType = impactType,
            EstimatedImpact = estimatedImpact,
            ConfidenceLevel = confidenceLevel,
            Observation = observation,
            MeasurementReferenceUrl = measurementReferenceUrl
        };

    public static DemandKpiLink FromRepository(
        Guid demandId,
        Guid kpiId,
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel,
        string? observation,
        string? measurementReferenceUrl = null) =>
        Create(demandId, kpiId, impactType, estimatedImpact, confidenceLevel, observation, measurementReferenceUrl);

    public void Update(
        ImpactType impactType,
        decimal? estimatedImpact,
        ConfidenceLevel confidenceLevel,
        string? observation,
        string? measurementReferenceUrl = null)
    {
        ImpactType = impactType;
        EstimatedImpact = estimatedImpact;
        ConfidenceLevel = confidenceLevel;
        Observation = observation;
        MeasurementReferenceUrl = measurementReferenceUrl;
    }
}
