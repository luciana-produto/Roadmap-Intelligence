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
        var demand = await demandRepository.GetByIdAsync(request.DemandId, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.DemandId);

        var scopedDemands = await demandRepository.GetByScopeTrackedAsync(
            demand.ProjectId,
            demand.QuarterYear,
            demand.QuarterNumber,
            cancellationToken);

        var demandIds = scopedDemands.Select(item => item.Id).ToHashSet();
        var requestedIds = request.OrderedDemandIds.ToHashSet();
        if (demandIds.Count != requestedIds.Count || !demandIds.SetEquals(requestedIds))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.OrderedDemandIds), "OrderedDemandIds must match the current demand scope.")
            ]);
        }

        var targetDemand = scopedDemands.First(item => item.Id == request.DemandId);
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

        var scopedDemandMap = scopedDemands.ToDictionary(item => item.Id);
        for (var index = 0; index < request.OrderedDemandIds.Count; index++)
        {
            var demandId = request.OrderedDemandIds[index];
            scopedDemandMap[demandId].SetSortOrder((index + 1) * 10);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}