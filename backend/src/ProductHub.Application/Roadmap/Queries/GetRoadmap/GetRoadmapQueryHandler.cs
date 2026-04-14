using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetRoadmap;

public sealed class GetDemandsQueryHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository)
    : IRequestHandler<GetDemandsQuery, IEnumerable<RoadmapDemandDto>>
{
    public async Task<IEnumerable<RoadmapDemandDto>> Handle(
        GetDemandsQuery request,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdWithProductsAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        var productMap = project.Products.ToDictionary(p => p.Id, p => p.Name);
        var projectNamesById = (await projectRepository.GetAllAsync(cancellationToken))
            .ToDictionary(projectItem => projectItem.Id, projectItem => projectItem.Name);

        var demands = await demandRepository.GetByProjectAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken);

        var demandIds = demands.Select(demand => demand.Id).ToArray();
        var dependencyLinks = await demandRepository.GetDependenciesByDemandIdsAsync(demandIds, cancellationToken);
        var relatedDemandIds = dependencyLinks
            .SelectMany(link => new[] { link.DemandId, link.DependsOnDemandId })
            .Distinct()
            .Except(demandIds)
            .ToArray();
        var relatedDemands = await demandRepository.GetByIdsAsync(relatedDemandIds, cancellationToken);
        var demandsById = demands
            .Concat(relatedDemands)
            .ToDictionary(demand => demand.Id, demand => demand);

        return demands.Select(demand =>
            RoadmapDemandDtoMapper.Map(demand, productMap, demandsById, projectNamesById, dependencyLinks));
    }
}
