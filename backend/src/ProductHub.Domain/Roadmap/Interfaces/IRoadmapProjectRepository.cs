using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Domain.Roadmap.Interfaces;

public interface IRoadmapProjectRepository : IRepository<RoadmapProject>
{
    Task<IEnumerable<RoadmapProject>> GetAllWithProductsAsync(CancellationToken cancellationToken = default);
    Task<RoadmapProject?> GetByIdWithProductsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoadmapProject?> GetByIdTrackedWithProductsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasLinkedDataAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> IsProductInUseAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<bool> ProductNameExistsAsync(Guid projectId, string name, Guid? excludeProductId = null, CancellationToken cancellationToken = default);
    Task<RoadmapProduct?> GetProductTrackedAsync(Guid projectId, Guid productId, CancellationToken cancellationToken = default);
    Task AddProductAsync(RoadmapProduct product, CancellationToken cancellationToken = default);
    void RemoveProduct(RoadmapProduct product);
}
