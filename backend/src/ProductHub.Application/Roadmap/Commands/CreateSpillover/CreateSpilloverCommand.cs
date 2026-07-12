using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateSpillover;

public sealed record CreateSpilloverCommand(
    Guid OriginalDemandId,
    int TargetQuarterYear,
    int TargetQuarterNumber,
    string? SpilloverReason = null,
    string? SpilloverObservation = null) : IRequest<RoadmapDemandDto>;
