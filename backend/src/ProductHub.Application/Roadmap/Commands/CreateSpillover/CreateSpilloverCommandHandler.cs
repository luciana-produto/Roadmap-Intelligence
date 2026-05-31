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

        if (original.ItemType != RoadmapItemType.Demand)
            throw new ValidationException([new ValidationFailure(nameof(request.OriginalDemandId), "Only demand items can have a spillover.")]);

        if (original.SuccessorDemandId.HasValue)
            throw new ValidationException([new ValidationFailure(nameof(request.OriginalDemandId), "This demand already has a spillover.")]);

        var nextSortOrder = original.ProjectId.HasValue
            ? await demandRepository.GetNextSortOrderAsync(
                original.ProjectId.Value,
                request.TargetQuarterYear,
                request.TargetQuarterNumber,
                cancellationToken)
            : 0;

        var spillover = RoadmapDemand.Create(
            RoadmapItemType.Demand,
            original.ParentDemandId,
            original.Title,
            original.Description,
            original.ProjectId,
            null,
            request.TargetQuarterYear,
            request.TargetQuarterNumber,
            DemandStatus.Backlog,
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
        if (original.ProjectId.HasValue)
        {
            var project = await projectRepository.GetByIdWithProductsAsync(original.ProjectId.Value, cancellationToken);
            if (project is not null)
                productNamesById = project.Products.ToDictionary(p => p.Id, p => p.Name);
        }

        var dependencyLinks = await demandRepository.GetDependenciesByDemandIdsAsync([created.Id], cancellationToken);
        var tradeOffs = await kpiRepository.GetTradeOffsByDemandIdAsync(created.Id, cancellationToken);

        return RoadmapDemandDtoMapper.Map(created, productNamesById, demandsById, projectNamesById, dependencyLinks, tradeOffs: tradeOffs);
    }
}
