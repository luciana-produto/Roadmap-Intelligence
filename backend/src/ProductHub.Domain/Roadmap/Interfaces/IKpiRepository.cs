using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap.Interfaces;

public interface IKpiRepository : IRepository<Kpi>
{
    Task<IEnumerable<Kpi>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Kpi?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandKpiLink>> GetKpiLinksByDemandIdsAsync(IEnumerable<Guid> demandIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandKpiLink>> GetKpiLinksByKpiIdAsync(Guid kpiId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiMeasurement>> GetMeasurementsByKpiIdAsync(Guid kpiId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiMeasurement>> GetMeasurementsByDemandIdAsync(Guid demandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiMeasurement>> GetMeasurementsByDemandIdsAsync(IEnumerable<Guid> demandIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandTradeOff>> GetTradeOffsByDemandIdsAsync(IEnumerable<Guid> demandIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandTradeOff>> GetTradeOffsByDemandIdAsync(Guid demandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DemandTradeOff>> GetTradeOffsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task ReplaceDemandKpiLinksAsync(Guid demandId, IEnumerable<DemandKpiLink> links, CancellationToken cancellationToken = default);
    Task AddMeasurementAsync(KpiMeasurement measurement, CancellationToken cancellationToken = default);
    Task AddTradeOffAsync(DemandTradeOff tradeOff, CancellationToken cancellationToken = default);
    Task<DemandTradeOff?> GetTradeOffByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveTradeOff(DemandTradeOff tradeOff);
    Task<KpiMeasurement?> GetMeasurementByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveMeasurement(KpiMeasurement measurement);
}
