using MediatR;
using ProductHub.Application.Roadmap.Commands.UpdateDemand;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.BulkUpdateDemands;

/// <summary>
/// Applies several demand updates within a single HTTP request, reusing the regular update logic
/// for each item. This removes the per-item network round-trip that made bulk edits very slow.
/// </summary>
public sealed record BulkUpdateDemandsCommand(
    IReadOnlyList<UpdateRoadmapDemandCommand> Items) : IRequest<IReadOnlyList<RoadmapDemandDto>>;
