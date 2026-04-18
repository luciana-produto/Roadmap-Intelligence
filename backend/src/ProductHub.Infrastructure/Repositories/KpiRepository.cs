using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;
using ProductHub.Infrastructure.Persistence;

namespace ProductHub.Infrastructure.Repositories;

public sealed class KpiRepository(AppDbContext context)
    : Repository<Kpi>(context), IKpiRepository
{
    public async Task<IEnumerable<Kpi>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await context.Kpis
            .AsNoTracking()
            .Where(k => k.ProjectId == projectId)
            .OrderBy(k => k.Name)
            .ToListAsync(cancellationToken);

    public async Task<Kpi?> GetByIdTrackedAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await context.Kpis
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DemandKpiLink>> GetKpiLinksByDemandIdsAsync(
        IEnumerable<Guid> demandIds,
        CancellationToken cancellationToken = default)
    {
        var ids = demandIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        return await context.DemandKpiLinks
            .AsNoTracking()
            .Where(link => ids.Contains(link.DemandId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DemandKpiLink>> GetKpiLinksByKpiIdAsync(
        Guid kpiId,
        CancellationToken cancellationToken = default) =>
        await context.DemandKpiLinks
            .AsNoTracking()
            .Where(link => link.KpiId == kpiId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<KpiMeasurement>> GetMeasurementsByKpiIdAsync(
        Guid kpiId,
        CancellationToken cancellationToken = default) =>
        await context.KpiMeasurements
            .AsNoTracking()
            .Where(m => m.KpiId == kpiId)
            .OrderByDescending(m => m.MeasurementDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<KpiMeasurement>> GetMeasurementsByDemandIdAsync(
        Guid demandId,
        CancellationToken cancellationToken = default) =>
        await context.KpiMeasurements
            .AsNoTracking()
            .Where(m => m.DemandId == demandId)
            .OrderByDescending(m => m.MeasurementDate)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<KpiMeasurement>> GetMeasurementsByDemandIdsAsync(
        IEnumerable<Guid> demandIds,
        CancellationToken cancellationToken = default)
    {
        var ids = demandIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        return await context.KpiMeasurements
            .AsNoTracking()
            .Where(m => m.DemandId.HasValue && ids.Contains(m.DemandId.Value))
            .OrderByDescending(m => m.MeasurementDate)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DemandTradeOff>> GetTradeOffsByDemandIdsAsync(
        IEnumerable<Guid> demandIds,
        CancellationToken cancellationToken = default)
    {
        var ids = demandIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        return await context.DemandTradeOffs
            .AsNoTracking()
            .Where(t => ids.Contains(t.DeprioritizedDemandId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DemandTradeOff>> GetTradeOffsByDemandIdAsync(
        Guid demandId,
        CancellationToken cancellationToken = default) =>
        await context.DemandTradeOffs
            .AsNoTracking()
            .Where(t => t.DeprioritizedDemandId == demandId || t.ReplacementDemandId == demandId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DemandTradeOff>> GetTradeOffsByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await context.DemandTradeOffs
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public async Task ReplaceDemandKpiLinksAsync(
        Guid demandId,
        IEnumerable<DemandKpiLink> links,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.DemandKpiLinks
            .Where(l => l.DemandId == demandId)
            .ToListAsync(cancellationToken);

        context.DemandKpiLinks.RemoveRange(existing);

        foreach (var link in links)
            await context.DemandKpiLinks.AddAsync(link, cancellationToken);
    }

    public async Task AddMeasurementAsync(
        KpiMeasurement measurement,
        CancellationToken cancellationToken = default) =>
        await context.KpiMeasurements.AddAsync(measurement, cancellationToken);

    public async Task AddTradeOffAsync(
        DemandTradeOff tradeOff,
        CancellationToken cancellationToken = default) =>
        await context.DemandTradeOffs.AddAsync(tradeOff, cancellationToken);

    public async Task<DemandTradeOff?> GetTradeOffByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await context.DemandTradeOffs
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task RemoveTradeOff(DemandTradeOff tradeOff)
    {
        context.DemandTradeOffs.Remove(tradeOff);
        return Task.CompletedTask;
    }

    public async Task<KpiMeasurement?> GetMeasurementByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await context.KpiMeasurements
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task RemoveMeasurement(KpiMeasurement measurement)
    {
        context.KpiMeasurements.Remove(measurement);
        return Task.CompletedTask;
    }
}
