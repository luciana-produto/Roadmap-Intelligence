using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.ReorderDemand;

public sealed class ReorderRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReorderRoadmapDemandCommand, Unit>
{
    public async Task<Unit> Handle(
        ReorderRoadmapDemandCommand request,
        CancellationToken cancellationToken)
    {
        var demand = await demandRepository.GetByIdWithProductsAsync(request.DemandId, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.DemandId);

        var isSimpleEpic = demand.ItemType == RoadmapItemType.Epic && demand.IsSimple;
        if (demand.ItemType != RoadmapItemType.Demand && !isSimpleEpic)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.DemandId), "Only demand items and simple epics can be reordered within a quarter.")
            ]);
        }

        // Demands use ProjectId directly; simple epics store their project in ProjectLinks.
        var scopeProjectId = demand.ProjectId
            ?? demand.ProjectLinks.FirstOrDefault()?.ProjectId
            ?? throw new ValidationException([
                new ValidationFailure(nameof(request.DemandId), "Item has no associated project.")
            ]);

        var scopedDemands = await demandRepository.GetByScopeTrackedAsync(
            scopeProjectId,
            demand.QuarterYear,
            demand.QuarterNumber,
            cancellationToken);

        var scopedDemandMap = scopedDemands.ToDictionary(item => item.Id);

        // Tolerant reconciliation: apply the requested order to the items we recognize, then keep
        // any remaining scoped items (in their current order) right after. This keeps prioritizing
        // working even if the client's view of the scope drifted slightly — for example a concurrent
        // edit, or extra/missing items — instead of rejecting the whole operation.
        var orderedIds = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (var demandId in request.OrderedDemandIds)
        {
            if (scopedDemandMap.ContainsKey(demandId) && seen.Add(demandId))
                orderedIds.Add(demandId);
        }
        foreach (var scopedDemand in scopedDemands)
        {
            if (seen.Add(scopedDemand.Id))
                orderedIds.Add(scopedDemand.Id);
        }

        // Status changes still apply to the moved item, with the same guards.
        if (scopedDemandMap.TryGetValue(request.DemandId, out var targetDemand))
        {
            Enum.TryParse<DemandStatus>(request.Status, true, out var status);

            if (status == DemandStatus.Done && targetDemand.DeliveryDate is null)
            {
                throw new ValidationException([
                    new ValidationFailure(nameof(request.Status), "Delivery date is required when status is Done.")
                ]);
            }

            if (status == DemandStatus.Deprioritized
                && (string.IsNullOrWhiteSpace(targetDemand.Observation) || !targetDemand.DeprioritizationReason.HasValue))
            {
                throw new ValidationException([
                    new ValidationFailure(nameof(request.Status), "Deprioritization reason and observation are required when status is Deprioritized.")
                ]);
            }

            targetDemand.SetStatus(status);
        }

        for (var index = 0; index < orderedIds.Count; index++)
            scopedDemandMap[orderedIds[index]].SetSortOrder((index + 1) * 10);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}