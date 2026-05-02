using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateProject;

public sealed class CreateRoadmapProjectCommandHandler(
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRoadmapProjectCommand, RoadmapProjectDto>
{
    public async Task<RoadmapProjectDto> Handle(
        CreateRoadmapProjectCommand request,
        CancellationToken cancellationToken)
    {
        var slug = ProjectSlugGenerator.Generate(request.Name);

        if (await projectRepository.ExistsBySlugAsync(slug, cancellationToken: cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Name), "Já existe um projeto com um identificador interno equivalente.")
            ]);
        }

        var project = RoadmapProject.Create(request.Name, slug);
        await projectRepository.AddAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RoadmapProjectDtoMapper.Map(project);
    }
}