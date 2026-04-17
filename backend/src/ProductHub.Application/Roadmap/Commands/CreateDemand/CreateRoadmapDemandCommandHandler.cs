using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateDemand;

public sealed class CreateRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRoadmapDemandCommand, RoadmapDemandDto>
{
    public async Task<RoadmapDemandDto> Handle(
        CreateRoadmapDemandCommand request,
        CancellationToken cancellationToken)
    {
        var dependencyDemandIds = (request.DependencyDemandIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var project = await projectRepository.GetByIdWithProductsAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        var projectProductIds = project.Products.Select(p => p.Id).ToHashSet();
        foreach (var pid in request.ProductIds)
            if (!projectProductIds.Contains(pid))
                throw new NotFoundException("RoadmapProduct", pid);

        var dependencyDemands = await demandRepository.GetByIdsAsync(dependencyDemandIds, cancellationToken);
        var dependencyDemandMap = dependencyDemands.ToDictionary(demand => demand.Id);
        foreach (var dependencyDemandId in dependencyDemandIds)
            if (!dependencyDemandMap.ContainsKey(dependencyDemandId))
                throw new NotFoundException("RoadmapDemand", dependencyDemandId);

        Enum.TryParse<DemandType>(request.Type, true, out var type);
        Enum.TryParse<DemandClassification>(request.Classification, true, out var classification);
        var nextSortOrder = await demandRepository.GetNextSortOrderAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken);

        var demand = RoadmapDemand.Create(
            request.Title,
            request.Description,
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            type,
            classification,
            request.ProductIds,
            nextSortOrder,
            request.JiraIssue,
            request.Hours,
            request.Customers,
            request.IsBlocked,
            request.BlockedReason,
            request.PromisedDate,
            request.ProblemClarity,
            request.HasNoKpi);

        await demandRepository.AddAsync(demand, cancellationToken);
        await demandRepository.ReplaceDependenciesAsync(demand.Id, dependencyDemandIds, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var productMap = project.Products.ToDictionary(p => p.Id, p => p.Name);
        var projectNamesById = (await projectRepository.GetAllAsync(cancellationToken))
            .ToDictionary(projectItem => projectItem.Id, projectItem => projectItem.Name);
        var dependencyLinks = await demandRepository.GetDependenciesByDemandIdsAsync([demand.Id, .. dependencyDemandIds], cancellationToken);
        var demandsById = dependencyDemands
            .Concat([demand])
            .ToDictionary(item => item.Id, item => item);

        return RoadmapDemandDtoMapper.Map(demand, productMap, demandsById, projectNamesById, dependencyLinks);
    }
}
