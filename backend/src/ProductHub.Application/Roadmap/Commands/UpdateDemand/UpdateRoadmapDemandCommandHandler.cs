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

        var demand = await demandRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.Id);
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

        var dependencyDemands = await demandRepository.GetByIdsAsync(dependencyDemandIds, cancellationToken);
        var dependencyDemandMap = dependencyDemands.ToDictionary(item => item.Id);
        foreach (var dependencyDemandId in dependencyDemandIds)
            if (!dependencyDemandMap.ContainsKey(dependencyDemandId))
                throw new NotFoundException("RoadmapDemand", dependencyDemandId);

        Enum.TryParse<DemandStatus>(request.Status, true, out var status);
        Enum.TryParse<DemandType>(request.Type, true, out var type);
        Enum.TryParse<DemandClassification>(request.Classification, true, out var classification);
        NoKpiClassification? noKpiClassification = null;
        if (!string.IsNullOrWhiteSpace(request.NoKpiClassification))
        {
            Enum.TryParse<NoKpiClassification>(request.NoKpiClassification, true, out var parsedNoKpiClassification);
            noKpiClassification = parsedNoKpiClassification;
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

        demandRepository.Update(demand);
        await demandRepository.ReplaceProductsAsync(demand.Id, request.ProductIds, cancellationToken);
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
