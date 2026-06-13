using MediatR;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.BulkMoveDemands;

public sealed class BulkMoveDemandsToQuarterCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<BulkMoveDemandsToQuarterCommand, Unit>
{
    public async Task<Unit> Handle(
        BulkMoveDemandsToQuarterCommand request,
        CancellationToken cancellationToken)
    {
        var movedIds = (request.DemandIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        var orderedIds = (request.OrderedDemandIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var allIds = movedIds.Concat(orderedIds).Distinct().ToArray();
        if (allIds.Length == 0)
            return Unit.Value;

        var demands = await demandRepository.GetByIdsForUpdateAsync(allIds, cancellationToken);
        var demandsById = demands.ToDictionary(demand => demand.Id);

        // Move the selected demands to the target quarter.
        foreach (var id in movedIds)
            if (demandsById.TryGetValue(id, out var demand))
                demand.MoveToQuarter(request.TargetQuarterYear, request.TargetQuarterNumber);

        // Apply the requested order to the whole target scope (moved + already-present demands).
        for (var index = 0; index < orderedIds.Count; index++)
            if (demandsById.TryGetValue(orderedIds[index], out var demand))
                demand.SetSortOrder((index + 1) * 10);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
