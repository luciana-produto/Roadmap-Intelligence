using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetRoadmap;

public sealed class GetDemandsQueryHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository,
    IKpiRepository kpiRepository)
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

        var kpiLinks = await kpiRepository.GetKpiLinksByDemandIdsAsync(demandIds, cancellationToken);
        var kpiMeasurements = await kpiRepository.GetMeasurementsByDemandIdsAsync(demandIds, cancellationToken);
        var kpiIds = kpiLinks.Select(l => l.KpiId).Distinct().ToArray();
        var kpis = kpiIds.Length > 0
            ? (await kpiRepository.GetByProjectAsync(request.ProjectId, cancellationToken))
                .Where(k => kpiIds.Contains(k.Id))
                .ToDictionary(k => k.Id, k => k.Name)
            : new Dictionary<Guid, string>();

        return demands.Select(demand =>
            RoadmapDemandDtoMapper.Map(demand, productMap, demandsById, projectNamesById, dependencyLinks, kpis, kpiLinks, kpiMeasurements));
    }
}
