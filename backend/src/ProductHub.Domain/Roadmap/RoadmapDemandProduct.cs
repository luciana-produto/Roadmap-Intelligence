using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapDemandProduct : BaseEntity
{
    public Guid DemandId { get; private set; }
    public Guid ProductId { get; private set; }

    private RoadmapDemandProduct() { }

    internal static RoadmapDemandProduct Create(Guid demandId, Guid productId) =>
        new() { DemandId = demandId, ProductId = productId };

    public static RoadmapDemandProduct FromRepository(Guid demandId, Guid productId) =>
        Create(demandId, productId);
}
