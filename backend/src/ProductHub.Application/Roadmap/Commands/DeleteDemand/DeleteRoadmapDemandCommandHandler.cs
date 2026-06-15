using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemand;

public sealed class DeleteRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteRoadmapDemandCommand>
{
    public async Task Handle(DeleteRoadmapDemandCommand request, CancellationToken cancellationToken)
    {
        var demand = await demandRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.Id);

        if (demand.ItemType != RoadmapItemType.Demand
            && await demandRepository.HasChildrenAsync(request.Id, cancellationToken))
        {
            var message = demand.ItemType == RoadmapItemType.Roadmap
                ? "Este roadmap possui épicos vinculados e não pode ser removido."
                : "Este épico possui demandas vinculadas e não pode ser removido.";

            throw new ValidationException([
                new ValidationFailure(nameof(request.Id), message)
            ]);
        }

        // If this demand is a spillover, clear the SuccessorDemandId on the original
        var original = await demandRepository.GetOriginalBySuccessorIdAsync(request.Id, cancellationToken);
        original?.ClearSuccessor();

        // Remove every dependency link involving this item (both directions) so the deletion doesn't
        // violate the foreign key — the dependency is simply dropped from the other side.
        await demandRepository.RemoveAllDependenciesInvolvingAsync(request.Id, cancellationToken);

        demandRepository.Remove(demand);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
