using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
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

        demandRepository.Remove(demand);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
