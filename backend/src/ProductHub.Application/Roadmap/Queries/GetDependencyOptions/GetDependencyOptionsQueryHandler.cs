using MediatR;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetDependencyOptions;

public sealed class GetDemandDependencyOptionsQueryHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository)
    : IRequestHandler<GetDemandDependencyOptionsQuery, IEnumerable<DemandDependencyOptionDto>>
{
    public async Task<IEnumerable<DemandDependencyOptionDto>> Handle(
        GetDemandDependencyOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var projects = await projectRepository.GetAllAsync(cancellationToken);
        var demands = await demandRepository.GetAllAsync(cancellationToken);

        var projectNamesById = projects.ToDictionary(project => project.Id, project => project.Name);

        return demands
            .OrderBy(demand => projectNamesById.TryGetValue(demand.ProjectId, out var projectName) ? projectName : string.Empty)
            .ThenBy(demand => demand.Title)
            .Select(demand => new DemandDependencyOptionDto(
                demand.Id,
                demand.ProjectId,
                projectNamesById.TryGetValue(demand.ProjectId, out var projectName) ? projectName : string.Empty,
                demand.Title,
                demand.Quarter.Label,
                demand.QuarterYear,
                demand.QuarterNumber,
                demand.Status.ToString()));
    }
}