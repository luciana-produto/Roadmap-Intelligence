using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateSpillover;

public sealed class CreateSpilloverCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository,
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateSpilloverCommand, RoadmapDemandDto>
{
    public async Task<RoadmapDemandDto> Handle(
        CreateSpilloverCommand request,
        CancellationToken cancellationToken)
    {
        var original = await demandRepository.GetByIdForUpdateAsync(request.OriginalDemandId, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.OriginalDemandId);

        var isSimpleEpicSpillover = original.ItemType == RoadmapItemType.Epic && original.IsSimple;

        if (original.ItemType != RoadmapItemType.Demand && !isSimpleEpicSpillover)
            throw new ValidationException([new ValidationFailure(nameof(request.OriginalDemandId), "Only demand or simple epic items can have a spillover.")]);

        if (original.SuccessorDemandId.HasValue)
            throw new ValidationException([new ValidationFailure(nameof(request.OriginalDemandId), "This item already has a spillover.")]);

        var nextSortOrder = original.ProjectId.HasValue
            ? await demandRepository.GetNextSortOrderAsync(
                original.ProjectId.Value,
                request.TargetQuarterYear,
                request.TargetQuarterNumber,
                cancellationToken)
            : 0;

        var originalStatus = original.Status;

        var spillover = isSimpleEpicSpillover
            ? RoadmapDemand.Create(
                RoadmapItemType.Epic,
                original.ParentDemandId,
                original.Title,
                original.Description,
                null,
                original.ProjectLinks.Select(link => link.ProjectId),
                request.TargetQuarterYear,
                request.TargetQuarterNumber,
                originalStatus,
                DemandType.Spillover,
                original.Classification,
                original.Products.Select(p => p.ProductId),
                jiraIssue: original.JiraIssue,
                issueLinks: original.IssueLinks.Count > 0 ? original.IssueLinks : null,
                hours: original.Hours,
                customers: original.Customers,
                hasNoKpi: original.HasNoKpi,
                noKpiClassification: original.NoKpiClassification,
                isSimple: true)
            : RoadmapDemand.Create(
                RoadmapItemType.Demand,
                original.ParentDemandId,
                original.Title,
                original.Description,
                original.ProjectId,
                null,
                request.TargetQuarterYear,
                request.TargetQuarterNumber,
                originalStatus,
                DemandType.Spillover,
                original.Classification,
                original.Products.Select(p => p.ProductId),
                nextSortOrder,
                original.JiraIssue,
                original.IssueLinks.Count > 0 ? original.IssueLinks : null,
                original.Hours,
                original.Customers,
                hasNoKpi: original.HasNoKpi,
                noKpiClassification: original.NoKpiClassification);

        original.SetSuccessor(spillover.Id);
        original.SetStatus(DemandStatus.Spillover);
        original.SetSpilloverDetails(
            !string.IsNullOrEmpty(request.SpilloverReason) ? Enum.Parse<SpilloverReason>(request.SpilloverReason) : null,
            request.SpilloverObservation);

        await demandRepository.AddAsync(spillover, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await demandRepository.GetByIdWithProductsAsync(spillover.Id, cancellationToken) ?? spillover;

        var hierarchyDemandIds = new HashSet<Guid>();
        if (created.ParentDemandId.HasValue)
            hierarchyDemandIds.Add(created.ParentDemandId.Value);

        var hierarchyDemands = hierarchyDemandIds.Count > 0
            ? (await demandRepository.GetByIdsAsync(hierarchyDemandIds, cancellationToken)).ToList()
            : new List<RoadmapDemand>();

        var parentDemand = created.ParentDemandId.HasValue
            ? hierarchyDemands.FirstOrDefault(d => d.Id == created.ParentDemandId.Value)
            : null;

        if (parentDemand?.ParentDemandId.HasValue == true)
        {
            var ancestors = await demandRepository.GetByIdsAsync([parentDemand.ParentDemandId.Value], cancellationToken);
            hierarchyDemands = hierarchyDemands
                .Concat(ancestors)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();
        }

        var demandsById = hierarchyDemands
            .Append(created)
            .GroupBy(d => d.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var projectNamesById = (await projectRepository.GetAllAsync(cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

        Dictionary<Guid, string> productNamesById = [];
        // Demands carry a single ProjectId; simple epics carry their project via ProjectLinks.
        var productProjectIds = original.ProjectId.HasValue
            ? [original.ProjectId.Value]
            : original.ProjectLinks.Select(link => link.ProjectId).Distinct().ToArray();
        foreach (var productProjectId in productProjectIds)
        {
            var project = await projectRepository.GetByIdWithProductsAsync(productProjectId, cancellationToken);
            if (project is not null)
                foreach (var product in project.Products)
                    productNamesById.TryAdd(product.Id, product.Name);
        }

        var dependencyLinks = await demandRepository.GetDependenciesByDemandIdsAsync([created.Id], cancellationToken);
        var tradeOffs = await kpiRepository.GetTradeOffsByDemandIdAsync(created.Id, cancellationToken);

        return RoadmapDemandDtoMapper.Map(created, productNamesById, demandsById, projectNamesById, dependencyLinks, tradeOffs: tradeOffs);
    }
}
