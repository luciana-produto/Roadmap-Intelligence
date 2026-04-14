using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;
using ProductHub.Infrastructure.Persistence;

namespace ProductHub.Infrastructure.Repositories;

public sealed class RoadmapCapacityRepository(AppDbContext context)
    : Repository<RoadmapCapacity>(context), IRoadmapCapacityRepository
{
    public async Task<RoadmapCapacity?> GetByProjectQuarterAsync(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        CancellationToken cancellationToken = default) =>
        await context.RoadmapCapacities
            .FirstOrDefaultAsync(
                x => x.ProjectId == projectId
                    && x.QuarterYear == quarterYear
                    && x.QuarterNumber == quarterNumber,
                cancellationToken);
}