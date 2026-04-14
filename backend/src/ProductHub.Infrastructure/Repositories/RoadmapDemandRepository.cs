using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;
using ProductHub.Infrastructure.Persistence;
using ProductHub.Infrastructure.Repositories;

namespace ProductHub.Infrastructure.Repositories;

public sealed class RoadmapDemandRepository(AppDbContext context)
    : Repository<RoadmapDemand>(context), IRoadmapDemandRepository
{
    public async Task<IEnumerable<RoadmapDemand>> GetByProjectAsync(
        Guid projectId,
        int? quarterYear = null,
        int? quarterNumber = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RoadmapDemands
            .Include(d => d.Products)
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId);

        if (quarterYear.HasValue)
            query = query.Where(d => d.QuarterYear == quarterYear.Value);

        if (quarterNumber.HasValue)
            query = query.Where(d => d.QuarterNumber == quarterNumber.Value);

        return await query
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RoadmapDemand>> GetByScopeTrackedAsync(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapDemands
            .Include(d => d.Products)
            .Where(d => d.ProjectId == projectId && d.QuarterYear == quarterYear && d.QuarterNumber == quarterNumber)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextSortOrderAsync(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await context.RoadmapDemands
            .Where(d => d.ProjectId == projectId && d.QuarterYear == quarterYear && d.QuarterNumber == quarterNumber)
            .Select(d => (int?)d.SortOrder)
            .MaxAsync(cancellationToken);

        return (maxSortOrder ?? 0) + 10;
    }

    public async Task<RoadmapDemand?> GetByIdWithProductsAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapDemands
            .Include(d => d.Products)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RoadmapDemand>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var demandIds = ids.Distinct().ToArray();
        if (demandIds.Length == 0)
            return [];

        return await context.RoadmapDemands
            .Include(d => d.Products)
            .AsNoTracking()
            .Where(d => demandIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoadmapDemandDependency>> GetDependenciesByDemandIdsAsync(
        IEnumerable<Guid> demandIds,
        CancellationToken cancellationToken = default)
    {
        var ids = demandIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await context.RoadmapDemandDependencies
            .AsNoTracking()
            .Where(link => ids.Contains(link.DemandId) || ids.Contains(link.DependsOnDemandId))
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceProductsAsync(
        Guid demandId,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.RoadmapDemandProducts
            .Where(p => p.DemandId == demandId)
            .ToListAsync(cancellationToken);

        context.RoadmapDemandProducts.RemoveRange(existing);

        foreach (var pid in productIds.Distinct())
            await context.RoadmapDemandProducts.AddAsync(
                RoadmapDemandProduct.FromRepository(demandId, pid), cancellationToken);
    }

    public async Task ReplaceDependenciesAsync(
        Guid demandId,
        IEnumerable<Guid> dependsOnDemandIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.RoadmapDemandDependencies
            .Where(link => link.DemandId == demandId)
            .ToListAsync(cancellationToken);

        context.RoadmapDemandDependencies.RemoveRange(existing);

        foreach (var dependsOnDemandId in dependsOnDemandIds.Distinct())
        {
            await context.RoadmapDemandDependencies.AddAsync(
                RoadmapDemandDependency.FromRepository(demandId, dependsOnDemandId),
                cancellationToken);
        }
    }
}
