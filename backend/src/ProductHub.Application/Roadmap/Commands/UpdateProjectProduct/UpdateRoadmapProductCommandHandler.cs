using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpdateProjectProduct;

public sealed class UpdateRoadmapProductCommandHandler(
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRoadmapProductCommand, RoadmapProductDto>
{
    public async Task<RoadmapProductDto> Handle(
        UpdateRoadmapProductCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        var product = await projectRepository.GetProductTrackedAsync(request.ProjectId, request.ProductId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProduct", request.ProductId);

        if (await projectRepository.ProductNameExistsAsync(request.ProjectId, request.Name, request.ProductId, cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Name), "J\u00e1 existe um produto com este nome neste projeto.")
            ]);
        }

        product.Update(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RoadmapProductDto(product.Id, product.Name, product.ProjectId);
    }
}