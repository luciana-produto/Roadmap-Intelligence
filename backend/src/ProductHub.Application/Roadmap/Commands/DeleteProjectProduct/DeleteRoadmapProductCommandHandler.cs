using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteProjectProduct;

public sealed class DeleteRoadmapProductCommandHandler(
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteRoadmapProductCommand>
{
    public async Task Handle(DeleteRoadmapProductCommand request, CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        var product = await projectRepository.GetProductTrackedAsync(request.ProjectId, request.ProductId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProduct", request.ProductId);

        if (await projectRepository.IsProductInUseAsync(request.ProductId, cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.ProductId), "Este produto j\u00e1 est\u00e1 vinculado a demandas e n\u00e3o pode ser removido.")
            ]);
        }

        projectRepository.RemoveProduct(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}