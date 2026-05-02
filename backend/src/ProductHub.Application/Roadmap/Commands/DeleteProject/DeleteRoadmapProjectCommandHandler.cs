using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteProject;

public sealed class DeleteRoadmapProjectCommandHandler(
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteRoadmapProjectCommand>
{
    public async Task Handle(DeleteRoadmapProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdTrackedWithProductsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.Id);

        if (await projectRepository.HasLinkedDataAsync(request.Id, cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Id), "Este projeto j\u00e1 est\u00e1 em uso no roadmap e n\u00e3o pode ser removido.")
            ]);
        }

        projectRepository.Remove(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}