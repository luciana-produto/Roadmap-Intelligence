using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap;

public sealed class DemandTradeOff : BaseEntity, IAuditableEntity
{
    public Guid DeprioritizedDemandId { get; private set; }
    public Guid? ReplacementDemandId { get; private set; }
    public DeprioritizationReason Reason { get; private set; }
    public string? Observation { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private DemandTradeOff() { }

    public static DemandTradeOff Create(
        Guid deprioritizedDemandId,
        Guid? replacementDemandId,
        DeprioritizationReason reason,
        string? observation = null)
    {
        return new DemandTradeOff
        {
            DeprioritizedDemandId = deprioritizedDemandId,
            ReplacementDemandId = replacementDemandId,
            Reason = reason,
            Observation = observation?.Trim()
        };
    }

    public void Update(
        Guid? replacementDemandId,
        DeprioritizationReason reason,
        string? observation = null)
    {
        ReplacementDemandId = replacementDemandId;
        Reason = reason;
        Observation = observation?.Trim();
    }
}
