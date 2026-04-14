using ProductHub.Domain.Common;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapProduct : BaseEntity
{
    public string Name { get; private set; } = default!;
    public Guid ProjectId { get; private set; }

    private RoadmapProduct() { }

    internal static RoadmapProduct Create(string name, Guid projectId) =>
        new() { Name = name, ProjectId = projectId };
}
