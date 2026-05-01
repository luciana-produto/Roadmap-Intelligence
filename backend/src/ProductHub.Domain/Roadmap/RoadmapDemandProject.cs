using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapDemandProject : BaseEntity
{
    public Guid DemandId { get; private set; }
    public Guid ProjectId { get; private set; }

    private RoadmapDemandProject() { }

    internal static RoadmapDemandProject Create(Guid demandId, Guid projectId) =>
        new() { DemandId = demandId, ProjectId = projectId };

    public static RoadmapDemandProject FromRepository(Guid demandId, Guid projectId) =>
        Create(demandId, projectId);
}