using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.PurgeDemand;

public sealed class PurgeRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PurgeRoadmapDemandCommand>
{
    public async Task Handle(PurgeRoadmapDemandCommand request, CancellationToken cancellationToken)
    {
        var demand = await demandRepository.GetByIdIncludingDeletedAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.Id);

        if (!demand.IsDeleted)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Id),
                    "Só é possível excluir definitivamente itens que estão na lixeira.")
            ]);
        }

        // Limpeza de integridade referencial (igual à antiga exclusão física).
        var original = await demandRepository.GetOriginalBySuccessorIdAsync(request.Id, cancellationToken);
        original?.ClearSuccessor();

        await demandRepository.RemoveAllDependenciesInvolvingAsync(request.Id, cancellationToken);

        demandRepository.Remove(demand);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
