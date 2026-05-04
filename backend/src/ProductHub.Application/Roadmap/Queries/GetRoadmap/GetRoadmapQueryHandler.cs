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
        var projects = (await projectRepository.GetAllWithProductsAsync(cancellationToken)).ToList();
        if (request.ProjectId.HasValue && projects.All(projectItem => projectItem.Id != request.ProjectId.Value))
            throw new NotFoundException("RoadmapProject", request.ProjectId.Value);

        var productMap = projects
            .SelectMany(project => project.Products)
            .GroupBy(product => product.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var projectNamesById = projects.ToDictionary(projectItem => projectItem.Id, projectItem => projectItem.Name);

        var projectScopedDemands = (await demandRepository.GetByProjectAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken))
            .ToList();

        var ancestorIds = new HashSet<Guid>(
            projectScopedDemands
                .Where(demand => demand.ParentDemandId.HasValue)
                .Select(demand => demand.ParentDemandId!.Value));

        var directAncestors = await demandRepository.GetByIdsAsync(ancestorIds, cancellationToken);
        foreach (var grandParentId in directAncestors
            .Where(item => item.ParentDemandId.HasValue)
            .Select(item => item.ParentDemandId!.Value))
        {
            ancestorIds.Add(grandParentId);
        }

        var ancestors = await demandRepository.GetByIdsAsync(ancestorIds, cancellationToken);
        var demands = projectScopedDemands
            .Concat(ancestors)
            .GroupBy(demand => demand.Id)
            .Select(group => group.First())
            .ToList();

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
        var tradeOffs = await kpiRepository.GetTradeOffsByDemandIdsAsync(demandIds, cancellationToken);
        var kpiIds = kpiLinks.Select(l => l.KpiId).Distinct().ToArray();
        var kpis = kpiIds.Length > 0
            ? (await kpiRepository.GetAllAsync(cancellationToken))
                .Where(k => kpiIds.Contains(k.Id))
                .ToDictionary(k => k.Id, k => k.Name)
            : new Dictionary<Guid, string>();

        return demands.Select(demand =>
            RoadmapDemandDtoMapper.Map(demand, productMap, demandsById, projectNamesById, dependencyLinks, kpis, kpiLinks, kpiMeasurements, tradeOffs));
    }
}
