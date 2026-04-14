using MediatR;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetProjects;

public sealed class GetProjectsQueryHandler(IRoadmapProjectRepository repository)
    : IRequestHandler<GetProjectsQuery, IEnumerable<RoadmapProjectDto>>
{
    public async Task<IEnumerable<RoadmapProjectDto>> Handle(
        GetProjectsQuery request,
        CancellationToken cancellationToken)
    {
        var projects = await repository.GetAllWithProductsAsync(cancellationToken);

        return projects.Select(p => new RoadmapProjectDto(
            p.Id,
            p.Name,
            p.Slug,
            p.Products
                .Select(pr => new RoadmapProductDto(pr.Id, pr.Name, pr.ProjectId))
                .ToList()
                .AsReadOnly()));
    }
}
