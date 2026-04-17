using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.DTOs;

internal static class RoadmapDemandDtoMapper
{
    public static RoadmapDemandDto Map(
        RoadmapDemand demand,
        IReadOnlyDictionary<Guid, string> productNamesById,
        IReadOnlyDictionary<Guid, RoadmapDemand> demandsById,
        IReadOnlyDictionary<Guid, string> projectNamesById,
        IEnumerable<RoadmapDemandDependency> dependencyLinks,
        IReadOnlyDictionary<Guid, string>? kpiNamesById = null,
        IEnumerable<DemandKpiLink>? kpiLinks = null)
    {
        var effectivePromisedDate = GetEffectivePromisedDate(demand);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var isDeliveredLate = demand.Status == DemandStatus.Done
            && demand.DeliveryDate.HasValue
            && effectivePromisedDate.HasValue
            && demand.DeliveryDate.Value > effectivePromisedDate.Value;
        var isOverdue = demand.Status is not DemandStatus.Done and not DemandStatus.Deprioritized
            && effectivePromisedDate.HasValue
            && today > effectivePromisedDate.Value;

        var dependsOn = dependencyLinks
            .Where(link => link.DemandId == demand.Id)
            .Select(link => MapDependency(link.DependsOnDemandId, demandsById, projectNamesById))
            .Where(dto => dto is not null)
            .Cast<DemandDependencyDto>()
            .ToList()
            .AsReadOnly();

        var dependedOnBy = dependencyLinks
            .Where(link => link.DependsOnDemandId == demand.Id)
            .Select(link => MapDependency(link.DemandId, demandsById, projectNamesById))
            .Where(dto => dto is not null)
            .Cast<DemandDependencyDto>()
            .ToList()
            .AsReadOnly();

        var kpiLinkDtos = (kpiLinks ?? [])
            .Where(link => link.DemandId == demand.Id)
            .Select(link => new DemandKpiLinkDto(
                link.Id,
                link.DemandId,
                link.KpiId,
                kpiNamesById != null && kpiNamesById.TryGetValue(link.KpiId, out var kpiName) ? kpiName : string.Empty,
                link.ImpactType.ToString(),
                link.EstimatedImpact,
                link.ConfidenceLevel.ToString()))
            .ToList()
            .AsReadOnly();

        return new RoadmapDemandDto(
            demand.Id,
            demand.Title,
            demand.Description,
            demand.ProjectId,
            demand.Quarter.Label,
            demand.QuarterYear,
            demand.QuarterNumber,
            demand.SortOrder,
            demand.Status.ToString(),
            demand.Type.ToString(),
            demand.Classification.ToString(),
            demand.Products
                .Select(product => new DemandProductDto(
                    product.ProductId,
                    productNamesById.TryGetValue(product.ProductId, out var productName) ? productName : string.Empty))
                .ToList()
                .AsReadOnly(),
            demand.Observation,
            demand.JiraIssue,
            demand.Hours,
            demand.Customers,
            demand.IsBlocked,
            demand.BlockedReason,
            dependsOn,
            dependedOnBy,
            demand.PromisedDate,
            effectivePromisedDate,
            demand.DeliveryDate,
            isOverdue,
            isDeliveredLate,
            demand.ProblemClarity,
            demand.HasNoKpi,
            kpiLinkDtos,
            demand.CreatedAt,
            demand.UpdatedAt);
    }

    private static DateOnly? GetEffectivePromisedDate(RoadmapDemand demand)
    {
        if (demand.PromisedDate.HasValue)
            return demand.PromisedDate;

        if (demand.QuarterYear <= 0 || demand.QuarterNumber <= 0)
            return null;

        var month = demand.QuarterNumber * 3;
        var lastDay = DateTime.DaysInMonth(demand.QuarterYear, month);
        return new DateOnly(demand.QuarterYear, month, lastDay);
    }

    private static DemandDependencyDto? MapDependency(
        Guid demandId,
        IReadOnlyDictionary<Guid, RoadmapDemand> demandsById,
        IReadOnlyDictionary<Guid, string> projectNamesById)
    {
        if (!demandsById.TryGetValue(demandId, out var relatedDemand))
            return null;

        return new DemandDependencyDto(
            relatedDemand.Id,
            relatedDemand.ProjectId,
            projectNamesById.TryGetValue(relatedDemand.ProjectId, out var projectName) ? projectName : string.Empty,
            relatedDemand.Title,
            relatedDemand.Quarter.Label,
            relatedDemand.QuarterYear,
            relatedDemand.QuarterNumber,
            relatedDemand.Status.ToString());
    }
}