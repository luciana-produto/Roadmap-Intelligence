using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Domain.Roadmap.Interfaces;

public interface IRoadmapDemandRepository : IRepository<RoadmapDemand>
{
    Task<IEnumerable<RoadmapDemand>> GetByProjectAsync(
        Guid projectId,
        int? quarterYear = null,
        int? quarterNumber = null,
        CancellationToken cancellationToken = default);

    Task<List<RoadmapDemand>> GetByScopeTrackedAsync(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        CancellationToken cancellationToken = default);

    Task<int> GetNextSortOrderAsync(
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        CancellationToken cancellationToken = default);

    Task<RoadmapDemand?> GetByIdWithProductsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RoadmapDemand?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoadmapDemand>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoadmapDemandDependency>> GetDependenciesByDemandIdsAsync(
        IEnumerable<Guid> demandIds,
        CancellationToken cancellationToken = default);

    Task ReplaceProductsAsync(Guid demandId, IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);

    Task ReplaceProjectLinksAsync(Guid demandId, IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default);

    Task ReplaceDependenciesAsync(
        Guid demandId,
        IEnumerable<Guid> dependsOnDemandIds,
        CancellationToken cancellationToken = default);
}
