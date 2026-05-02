using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateProjectProduct;

public sealed class CreateRoadmapProductCommandHandler(
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRoadmapProductCommand, RoadmapProductDto>
{
    public async Task<RoadmapProductDto> Handle(
        CreateRoadmapProductCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        if (await projectRepository.ProductNameExistsAsync(request.ProjectId, request.Name, cancellationToken: cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Name), "J\u00e1 existe um produto com este nome neste projeto.")
            ]);
        }

        var product = RoadmapProduct.Create(request.Name, request.ProjectId);
        await projectRepository.AddProductAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RoadmapProductDto(product.Id, product.Name, product.ProjectId);
    }
}