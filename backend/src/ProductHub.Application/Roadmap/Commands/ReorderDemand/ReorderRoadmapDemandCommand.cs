using MediatR;

namespace ProductHub.Application.Roadmap.Commands.ReorderDemand;

public sealed record ReorderRoadmapDemandCommand(
    Guid DemandId,
    string Status,
    IReadOnlyList<Guid> OrderedDemandIds) : IRequest<Unit>;
