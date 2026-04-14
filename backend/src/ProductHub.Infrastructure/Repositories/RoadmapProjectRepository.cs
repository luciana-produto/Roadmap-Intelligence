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
}
