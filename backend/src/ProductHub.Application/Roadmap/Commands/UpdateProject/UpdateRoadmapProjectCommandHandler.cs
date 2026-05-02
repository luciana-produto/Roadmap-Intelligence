using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpdateProject;

public sealed class UpdateRoadmapProjectCommandHandler(
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRoadmapProjectCommand, RoadmapProjectDto>
{
    public async Task<RoadmapProjectDto> Handle(
        UpdateRoadmapProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdTrackedWithProductsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.Id);

        var slug = ProjectSlugGenerator.Generate(request.Name);

        if (await projectRepository.ExistsBySlugAsync(slug, request.Id, cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Name), "Já existe um projeto com um identificador interno equivalente.")
            ]);
        }

        project.Update(request.Name, slug);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RoadmapProjectDtoMapper.Map(project);
    }
}