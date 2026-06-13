using MediatR;

namespace ProductHub.Application.Roadmap.Commands.BulkMoveDemands;

/// <summary>
/// Moves a set of demands to a target quarter and applies a single ordering to the whole target
/// scope, in one transaction. Used to move an epic's demands between quarters without the visual
/// flicker of issuing one request per demand. <paramref name="OrderedDemandIds"/> is the desired
/// final order of the target quarter scope (the moved demands plus the demands already there).
/// </summary>
public sealed record BulkMoveDemandsToQuarterCommand(
    IReadOnlyList<Guid> DemandIds,
    int TargetQuarterYear,
    int TargetQuarterNumber,
    IReadOnlyList<Guid> OrderedDemandIds) : IRequest<Unit>;
