using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap;

public sealed class KpiMeasurement : BaseEntity, IAuditableEntity
{
    public Guid KpiId { get; private set; }
    public Guid? DemandId { get; private set; }
    public decimal MeasuredValue { get; private set; }
    public DateOnly MeasurementDate { get; private set; }
    public MeasurementResult Result { get; private set; }
    public string? Observation { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private KpiMeasurement() { }

    public static KpiMeasurement Create(
        Guid kpiId,
        Guid? demandId,
        decimal measuredValue,
        DateOnly measurementDate,
        MeasurementResult result,
        string? observation = null)
    {
        return new KpiMeasurement
        {
            KpiId = kpiId,
            DemandId = demandId,
            MeasuredValue = measuredValue,
            MeasurementDate = measurementDate,
            Result = result,
            Observation = observation?.Trim()
        };
    }

    public void Update(
        decimal measuredValue,
        DateOnly measurementDate,
        MeasurementResult result,
        string? observation = null)
    {
        MeasuredValue = measuredValue;
        MeasurementDate = measurementDate;
        Result = result;
        Observation = observation?.Trim();
    }
}
