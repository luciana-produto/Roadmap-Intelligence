using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemand;

public sealed class UpdateRoadmapDemandCommandHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository,
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRoadmapDemandCommand, RoadmapDemandDto>
{
    public async Task<RoadmapDemandDto> Handle(
        UpdateRoadmapDemandCommand request,
        CancellationToken cancellationToken)
    {
        var dependencyDemandIds = (request.DependencyDemandIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var relatedDemandIds = request.ReplacementDemandId.HasValue
            ? [.. dependencyDemandIds, request.ReplacementDemandId.Value]
            : dependencyDemandIds;

        var demand = await demandRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.Id);
        var originalStatus = demand.Status;
        var originalQuarterYear = demand.QuarterYear;
        var originalQuarterNumber = demand.QuarterNumber;

        if (request.ProjectId != demand.ProjectId)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.ProjectId), "Changing the project of an existing demand is not supported.")
            ]);
        }

        var project = await projectRepository.GetByIdWithProductsAsync(demand.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", demand.ProjectId);

        var projectProductIds = project.Products.Select(p => p.Id).ToHashSet();
        foreach (var pid in request.ProductIds)
            if (!projectProductIds.Contains(pid))
                throw new NotFoundException("RoadmapProduct", pid);

        if (dependencyDemandIds.Contains(request.Id))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.DependencyDemandIds), "A demand cannot depend on itself.")
            ]);
        }

        var dependencyDemands = await demandRepository.GetByIdsAsync(relatedDemandIds, cancellationToken);
        var dependencyDemandMap = dependencyDemands.ToDictionary(item => item.Id);
        foreach (var dependencyDemandId in dependencyDemandIds)
            if (!dependencyDemandMap.ContainsKey(dependencyDemandId))
                throw new NotFoundException("RoadmapDemand", dependencyDemandId);
        if (request.ReplacementDemandId.HasValue && !dependencyDemandMap.ContainsKey(request.ReplacementDemandId.Value))
            throw new NotFoundException("RoadmapDemand", request.ReplacementDemandId.Value);

        Enum.TryParse<DemandStatus>(request.Status, true, out var status);
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

        int? nextSortOrder = null;
        if (originalQuarterYear != request.QuarterYear || originalQuarterNumber != request.QuarterNumber)
        {
            nextSortOrder = await demandRepository.GetNextSortOrderAsync(
                demand.ProjectId,
                request.QuarterYear,
                request.QuarterNumber,
                cancellationToken);
        }

        demand.Update(
            request.Title,
            request.Description,
            request.QuarterYear,
            request.QuarterNumber,
            status,
            type,
            classification,
            nextSortOrder,
            request.Observation,
            deprioritizationReason,
            request.ReplacementDemandId,
            request.JiraIssue,
            request.Hours,
            request.Customers,
            request.IsBlocked,
            request.BlockedReason,
            request.PromisedDate,
            request.DeliveryDate,
            request.ProblemClarity,
            request.HasNoKpi,
            noKpiClassification);

        if (status == DemandStatus.Deprioritized && deprioritizationReason.HasValue)
        {
            var existingTradeOffs = await kpiRepository.GetTradeOffsByDemandIdAsync(demand.Id, cancellationToken);
            var matchingTradeOffId = existingTradeOffs
                .Where(tradeOff => tradeOff.DeprioritizedDemandId == demand.Id)
                .Where(tradeOff => tradeOff.ProjectId == demand.ProjectId)
                .Where(tradeOff => tradeOff.QuarterYear == request.QuarterYear)
                .Where(tradeOff => tradeOff.QuarterNumber == request.QuarterNumber)
                .OrderByDescending(tradeOff => tradeOff.CreatedAt)
                .Select(tradeOff => tradeOff.Id)
                .FirstOrDefault();

            var shouldUpdateExistingTradeOff = originalStatus == DemandStatus.Deprioritized
                && originalQuarterYear == request.QuarterYear
                && originalQuarterNumber == request.QuarterNumber
                && matchingTradeOffId != Guid.Empty;

            if (shouldUpdateExistingTradeOff)
            {
                var existingTradeOff = await kpiRepository.GetTradeOffByIdAsync(matchingTradeOffId, cancellationToken);
                existingTradeOff?.Update(request.ReplacementDemandId, deprioritizationReason.Value, request.Observation);
            }
            else
            {
                await kpiRepository.AddTradeOffAsync(
                    DemandTradeOff.Create(
                        demand.ProjectId,
                        request.QuarterYear,
                        request.QuarterNumber,
                        demand.Id,
                        request.ReplacementDemandId,
                        deprioritizationReason.Value,
                        request.Observation),
                    cancellationToken);
            }
        }

        demandRepository.Update(demand);
        await demandRepository.ReplaceProductsAsync(demand.Id, request.ProductIds, cancellationToken);
        await demandRepository.ReplaceDependenciesAsync(demand.Id, dependencyDemandIds, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var productMap = project.Products.ToDictionary(p => p.Id, p => p.Name);
        var projectNamesById = (await projectRepository.GetAllAsync(cancellationToken))
            .ToDictionary(projectItem => projectItem.Id, projectItem => projectItem.Name);
        var dependencyLinks = await demandRepository.GetDependenciesByDemandIdsAsync([demand.Id, .. dependencyDemandIds], cancellationToken);
        var tradeOffs = await kpiRepository.GetTradeOffsByDemandIdAsync(demand.Id, cancellationToken);
        var tradeOffRelatedDemandIds = tradeOffs
            .Where(tradeOff => tradeOff.ReplacementDemandId.HasValue)
            .Select(tradeOff => tradeOff.ReplacementDemandId!.Value)
            .Where(replacementDemandId => replacementDemandId != demand.Id)
            .Except(dependencyDemands.Select(item => item.Id))
            .ToArray();
        var tradeOffRelatedDemands = await demandRepository.GetByIdsAsync(tradeOffRelatedDemandIds, cancellationToken);
        var demandsById = dependencyDemands
            .Concat(tradeOffRelatedDemands)
            .Concat([demand])
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        return RoadmapDemandDtoMapper.Map(demand, productMap, demandsById, projectNamesById, dependencyLinks, tradeOffs: tradeOffs);
    }
}
