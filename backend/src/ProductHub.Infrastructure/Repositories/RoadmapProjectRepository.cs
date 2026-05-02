using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Common;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;
using ProductHub.Infrastructure.Persistence;
using ProductHub.Infrastructure.Repositories;

namespace ProductHub.Infrastructure.Repositories;

public sealed class RoadmapProjectRepository(AppDbContext context)
    : Repository<RoadmapProject>(context), IRoadmapProjectRepository
{
    public async Task<IEnumerable<RoadmapProject>> GetAllWithProductsAsync(
        CancellationToken cancellationToken = default) =>
        await context.RoadmapProjects
            .Include(p => p.Products)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<RoadmapProject?> GetByIdWithProductsAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapProjects
            .Include(p => p.Products)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<RoadmapProject?> GetByIdTrackedWithProductsAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapProjects
            .Include(p => p.Products)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<bool> ExistsBySlugAsync(
        string slug,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapProjects
            .AsNoTracking()
            .AnyAsync(
                project => project.Slug == slug
                    && (!excludeId.HasValue || project.Id != excludeId.Value),
                cancellationToken);

    public async Task<bool> HasLinkedDataAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var hasDemandLinks = await context.RoadmapDemands
            .AsNoTracking()
            .AnyAsync(
                demand => demand.ProjectId == projectId
                    || demand.ProjectLinks.Any(link => link.ProjectId == projectId),
                cancellationToken);

        if (hasDemandLinks)
            return true;

        var hasCapacityLinks = await context.RoadmapCapacities
            .AsNoTracking()
            .AnyAsync(capacity => capacity.ProjectId == projectId, cancellationToken);

        if (hasCapacityLinks)
            return true;

        var hasKpiLinks = await context.Kpis
            .AsNoTracking()
            .AnyAsync(kpi => kpi.ProjectId == projectId, cancellationToken);

        if (hasKpiLinks)
            return true;

        var hasTradeOffLinks = await context.DemandTradeOffs
            .AsNoTracking()
            .AnyAsync(tradeOff => tradeOff.ProjectId == projectId, cancellationToken);

        if (hasTradeOffLinks)
            return true;

        return await context.RoadmapDemandProducts
            .AsNoTracking()
            .AnyAsync(
                link => context.RoadmapProducts
                    .Where(product => product.ProjectId == projectId)
                    .Select(product => product.Id)
                    .Contains(link.ProductId),
                cancellationToken);
    }

    public async Task<bool> IsProductInUseAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapDemandProducts
            .AsNoTracking()
            .AnyAsync(link => link.ProductId == productId, cancellationToken);

    public async Task<bool> ProductNameExistsAsync(
        Guid projectId,
        string name,
        Guid? excludeProductId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();

        return await context.RoadmapProducts
            .AsNoTracking()
            .AnyAsync(
                product => product.ProjectId == projectId
                    && product.Name == normalizedName
                    && (!excludeProductId.HasValue || product.Id != excludeProductId.Value),
                cancellationToken);
    }

    public async Task<RoadmapProduct?> GetProductTrackedAsync(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapProducts
            .FirstOrDefaultAsync(
                product => product.ProjectId == projectId && product.Id == productId,
                cancellationToken);

    public async Task AddProductAsync(
        RoadmapProduct product,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapProducts.AddAsync(product, cancellationToken);

    public void RemoveProduct(RoadmapProduct product) =>
        context.RoadmapProducts.Remove(product);
}
