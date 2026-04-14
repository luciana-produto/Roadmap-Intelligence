using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapDemandDependency : BaseEntity
{
    public Guid DemandId { get; private set; }
    public Guid DependsOnDemandId { get; private set; }

    private RoadmapDemandDependency() { }

    internal static RoadmapDemandDependency Create(Guid demandId, Guid dependsOnDemandId) =>
        new() { DemandId = demandId, DependsOnDemandId = dependsOnDemandId };

    public static RoadmapDemandDependency FromRepository(Guid demandId, Guid dependsOnDemandId) =>
        Create(demandId, dependsOnDemandId);
}