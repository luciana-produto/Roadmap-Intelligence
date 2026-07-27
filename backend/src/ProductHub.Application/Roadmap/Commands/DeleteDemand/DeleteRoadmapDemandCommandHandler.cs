using MediatR;
using ProductHub.Application.Common;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemand;

public sealed class DeleteRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteRoadmapDemandCommand>
{
    public async Task Handle(DeleteRoadmapDemandCommand request, CancellationToken cancellationToken)
    {
        // Rastreado (para atualizar); o filtro global garante que não é possível "excluir" o já excluído.
        var demand = await demandRepository.GetByIdForUpdateAsync(request.Id, cancellationToken)
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

        // Exclusão lógica: preserva dependências, spillover e vínculos para permitir a restauração.
        demand.SoftDelete(currentUser.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
