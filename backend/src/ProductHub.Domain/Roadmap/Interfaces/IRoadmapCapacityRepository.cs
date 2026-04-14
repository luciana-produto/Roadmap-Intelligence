using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap.Interfaces;

public interface IRoadmapCapacityRepository : IRepository<RoadmapCapacity>
{
    Task<RoadmapCapacity?> GetByProjectQuarterAsync(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        CancellationToken cancellationToken = default);
}