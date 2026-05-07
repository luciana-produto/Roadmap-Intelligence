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
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRoadmapDemandCommand, RoadmapDemandDto>
{
    public async Task<RoadmapDemandDto> Handle(
        CreateRoadmapDemandCommand request,
        CancellationToken cancellationToken)
    {
        Enum.TryParse<RoadmapItemType>(request.ItemType, true, out var itemType);
        Enum.TryParse<DemandStatus>(request.Status, true, out var status);
        var dependencyDemandIds = (request.DependencyDemandIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var replacementDemandId = status == DemandStatus.Deprioritized
            ? request.ReplacementDemandId
            : null;
        var relatedDemandIds = replacementDemandId.HasValue
            ? [.. dependencyDemandIds, replacementDemandId.Value]
            : dependencyDemandIds;

        RoadmapProject? project = null;
        Dictionary<Guid, string> productMap = [];
        if (request.ProjectId.HasValue)
        {
            project = await projectRepository.GetByIdWithProductsAsync(request.ProjectId.Value, cancellationToken)
                ?? throw new NotFoundException("RoadmapProject", request.ProjectId.Value);

            var projectProductIds = project.Products.Select(p => p.Id).ToHashSet();
            foreach (var pid in request.ProductIds)
                if (!projectProductIds.Contains(pid))
                    throw new NotFoundException("RoadmapProduct", pid);

            productMap = project.Products.ToDictionary(p => p.Id, p => p.Name);
        }

        if (itemType != RoadmapItemType.Demand && request.ProjectIds is { Count: > 0 })
        {
            var validProjectIds = (await projectRepository.GetAllAsync(cancellationToken))
                .Select(projectItem => projectItem.Id)
                .ToHashSet();

            foreach (var linkedProjectId in request.ProjectIds.Where(id => id != Guid.Empty).Distinct())
            {
                if (!validProjectIds.Contains(linkedProjectId))
                    throw new NotFoundException("RoadmapProject", linkedProjectId);
            }
        }

        if (request.ParentDemandId.HasValue)
        {
            var parent = await demandRepository.GetByIdAsync(request.ParentDemandId.Value, cancellationToken)
                ?? throw new NotFoundException("RoadmapDemand", request.ParentDemandId.Value);

            if (itemType == RoadmapItemType.Epic && parent.ItemType != RoadmapItemType.Roadmap)
                throw new ValidationException([new ValidationFailure(nameof(request.ParentDemandId), "An epic must be linked to a roadmap item.")]);

            if (itemType == RoadmapItemType.Demand && parent.ItemType != RoadmapItemType.Epic)
                throw new ValidationException([new ValidationFailure(nameof(request.ParentDemandId), "A demand must be linked to an epic item.")]);
        }

        var dependencyDemands = await demandRepository.GetByIdsAsync(relatedDemandIds, cancellationToken);
        var dependencyDemandMap = dependencyDemands.ToDictionary(demand => demand.Id);
        foreach (var dependencyDemandId in dependencyDemandIds)
            if (!dependencyDemandMap.ContainsKey(dependencyDemandId))
                throw new NotFoundException("RoadmapDemand", dependencyDemandId);
        if (replacementDemandId.HasValue && !dependencyDemandMap.ContainsKey(replacementDemandId.Value))
            throw new NotFoundException("RoadmapDemand", replacementDemandId.Value);

        Enum.TryParse<DemandType>(request.Type, true, out var type);
        Enum.TryParse<DemandClassification>(request.Classification, true, out var classification);
        NoKpiClassification? noKpiClassification = null;
        if (!string.IsNullOrWhiteSpace(request.NoKpiClassification))
        {
            Enum.TryParse<NoKpiClassification>(request.NoKpiClassification, true, out var parsedNoKpiClassification);
            noKpiClassification = parsedNoKpiClassification;
        }
        DeprioritizationReason? deprioritizationReason = null;
        if (!string.IsNullOrWhiteSpace(request.DeprioritizationReason))
        {
            Enum.TryParse<DeprioritizationReason>(request.DeprioritizationReason, true, out var parsedDeprioritizationReason);
            deprioritizationReason = parsedDeprioritizationReason;
        }

        var nextSortOrder = itemType == RoadmapItemType.Demand && request.ProjectId.HasValue
            ? await demandRepository.GetNextSortOrderAsync(
                request.ProjectId.Value,
                request.QuarterYear,
                request.QuarterNumber,
                cancellationToken)
            : 0;

        var demand = RoadmapDemand.Create(
            itemType,
            request.ParentDemandId,
            request.Title,
            request.Description,
            request.ProjectId,
            request.ProjectIds,
            request.QuarterYear,
            request.QuarterNumber,
            status,
            type,
            classification,
            request.ProductIds,
            nextSortOrder,
            request.JiraIssue,
                        request.IssueLinks?.Select(issue => RoadmapIssueLink.Create(issue.Key, issue.Url)),
            request.Hours,
            request.Customers,
            request.IsBlocked,
            request.BlockedReason,
            request.PromisedDate,
            request.Observation,
            deprioritizationReason,
            replacementDemandId,
            request.DeliveryDate,
            request.ProblemClarity,
            request.HasNoKpi,
            noKpiClassification);

        await demandRepository.AddAsync(demand, cancellationToken);
        await demandRepository.ReplaceDependenciesAsync(demand.Id, dependencyDemandIds, cancellationToken);

        if (status == DemandStatus.Deprioritized && deprioritizationReason.HasValue)
        {
            foreach (var tradeOffProjectId in GetAssociatedProjectIds(itemType, request.ProjectId, request.ProjectIds))
            {
                await kpiRepository.AddTradeOffAsync(
                    DemandTradeOff.Create(
                        tradeOffProjectId,
                        request.QuarterYear,
                        request.QuarterNumber,
                        demand.Id,
                        replacementDemandId,
                        deprioritizationReason.Value,
                        request.Observation),
                    cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var currentDemand = await demandRepository.GetByIdWithProductsAsync(demand.Id, cancellationToken)
            ?? demand;

        var hierarchyDemandIds = new HashSet<Guid>(dependencyDemandIds);
        if (currentDemand.ParentDemandId.HasValue)
            hierarchyDemandIds.Add(currentDemand.ParentDemandId.Value);

        var hierarchyDemands = (await demandRepository.GetByIdsAsync(hierarchyDemandIds, cancellationToken)).ToList();
        var parentDemand = currentDemand.ParentDemandId.HasValue
            ? hierarchyDemands.FirstOrDefault(item => item.Id == currentDemand.ParentDemandId.Value)
            : null;

        if (parentDemand?.ParentDemandId.HasValue == true)
        {
            var ancestorDemands = await demandRepository.GetByIdsAsync([parentDemand.ParentDemandId.Value], cancellationToken);
            hierarchyDemands = hierarchyDemands
                .Concat(ancestorDemands)
                .GroupBy(item => item.Id)
                .Select(group => group.First())
                .ToList();
        }

        var projectNamesById = (await projectRepository.GetAllAsync(cancellationToken))
            .ToDictionary(projectItem => projectItem.Id, projectItem => projectItem.Name);
        var dependencyLinks = await demandRepository.GetDependenciesByDemandIdsAsync([demand.Id, .. dependencyDemandIds], cancellationToken);
        var tradeOffs = await kpiRepository.GetTradeOffsByDemandIdAsync(currentDemand.Id, cancellationToken);
        var tradeOffRelatedDemandIds = tradeOffs
            .Where(tradeOff => tradeOff.ReplacementDemandId.HasValue)
            .Select(tradeOff => tradeOff.ReplacementDemandId!.Value)
            .Where(currentDemandId => currentDemandId != currentDemand.Id)
            .Except(dependencyDemands.Select(item => item.Id))
            .ToArray();
        var tradeOffRelatedDemands = await demandRepository.GetByIdsAsync(tradeOffRelatedDemandIds, cancellationToken);
        var demandsById = dependencyDemands
            .Concat(hierarchyDemands)
            .Concat(tradeOffRelatedDemands)
            .Concat([currentDemand])
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        return RoadmapDemandDtoMapper.Map(currentDemand, productMap, demandsById, projectNamesById, dependencyLinks, tradeOffs: tradeOffs);
    }

    private static IReadOnlyList<Guid> GetAssociatedProjectIds(
        RoadmapItemType itemType,
        Guid? projectId,
        IReadOnlyList<Guid>? projectIds)
    {
        if (itemType == RoadmapItemType.Demand)
            return projectId.HasValue ? [projectId.Value] : [];

        return (projectIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
    }
}
