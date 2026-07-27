using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.RestoreDemand;

public sealed class RestoreRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RestoreRoadmapDemandCommand>
{
    public async Task Handle(RestoreRoadmapDemandCommand request, CancellationToken cancellationToken)
    {
        var demand = await demandRepository.GetByIdIncludingDeletedAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.Id);

        if (!demand.IsDeleted)
            return; // já está ativo, nada a fazer

        // Não permite restaurar um item cujo pai (épico/roadmap) ainda está excluído — evitaria órfão oculto.
        if (demand.ParentDemandId.HasValue)
        {
            var parent = await demandRepository.GetByIdIncludingDeletedAsync(demand.ParentDemandId.Value, cancellationToken);
            if (parent is not null && parent.IsDeleted)
            {
                throw new ValidationException([
                    new ValidationFailure(nameof(request.Id),
                        "O item pai (épico/roadmap) também está excluído. Restaure o item pai primeiro.")
                ]);
            }
        }

        demand.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
