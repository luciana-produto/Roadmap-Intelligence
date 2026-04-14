using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Domain.Roadmap.Interfaces;

public interface IRoadmapProjectRepository : IRepository<RoadmapProject>
{
    Task<IEnumerable<RoadmapProject>> GetAllWithProductsAsync(CancellationToken cancellationToken = default);
    Task<RoadmapProject?> GetByIdWithProductsAsync(Guid id, CancellationToken cancellationToken = default);
}
