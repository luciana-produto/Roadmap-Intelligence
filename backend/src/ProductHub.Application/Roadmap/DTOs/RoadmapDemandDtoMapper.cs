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
        IEnumerable<DemandKpiLink>? kpiLinks = null,
        IEnumerable<KpiMeasurement>? kpiMeasurements = null,
        IEnumerable<DemandTradeOff>? tradeOffs = null)
    {
        var hierarchy = ResolveHierarchy(demand, demandsById);
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
                link.ConfidenceLevel.ToString(),
                link.Observation))
            .ToList()
            .AsReadOnly();

        var demandMeasurementDtos = (kpiMeasurements ?? [])
            .Where(measurement => measurement.DemandId == demand.Id)
            .OrderByDescending(measurement => measurement.MeasurementDate)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Select(measurement => new KpiMeasurementDto(
                measurement.Id,
                measurement.KpiId,
                kpiNamesById != null && kpiNamesById.TryGetValue(measurement.KpiId, out var measurementKpiName) ? measurementKpiName : string.Empty,
                measurement.DemandId,
                demand.Title,
                measurement.MeasuredValue,
                measurement.MeasurementDate,
                measurement.Result.ToString(),
                measurement.Observation,
                measurement.CreatedAt))
            .ToList()
            .AsReadOnly();

        var tradeOffHistoryDtos = (tradeOffs ?? [])
            .Where(tradeOff => tradeOff.DeprioritizedDemandId == demand.Id)
            .OrderByDescending(tradeOff => tradeOff.CreatedAt)
            .Select(tradeOff => new DemandTradeOffDto(
                tradeOff.Id,
                tradeOff.ProjectId,
                projectNamesById.TryGetValue(tradeOff.ProjectId, out var tradeOffProjectName) ? tradeOffProjectName : string.Empty,
                FormatQuarterLabel(tradeOff.QuarterYear, tradeOff.QuarterNumber),
                tradeOff.QuarterYear,
                tradeOff.QuarterNumber,
                tradeOff.DeprioritizedDemandId,
                demand.Title,
                tradeOff.ReplacementDemandId,
                tradeOff.ReplacementDemandId.HasValue && demandsById.TryGetValue(tradeOff.ReplacementDemandId.Value, out var replacementDemand)
                    ? replacementDemand.Title
                    : null,
                tradeOff.Reason.ToString(),
                tradeOff.Observation,
                tradeOff.CreatedAt))
            .ToList()
            .AsReadOnly();

        return new RoadmapDemandDto(
            demand.Id,
            demand.ItemType.ToString(),
            demand.ParentDemandId,
            hierarchy.ParentTitle,
            hierarchy.RoadmapId,
            hierarchy.RoadmapTitle,
            hierarchy.EpicId,
            hierarchy.EpicTitle,
            demand.Title,
            demand.Description,
            demand.ProjectId,
            (demand.ProjectId.HasValue
                ? demand.ProjectLinks.Select(link => link.ProjectId).Append(demand.ProjectId.Value)
                : demand.ProjectLinks.Select(link => link.ProjectId))
                .Distinct()
                .ToList()
                .AsReadOnly(),
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
            demand.DeprioritizationReason?.ToString(),
            demand.ReplacementDemandId,
            demand.JiraIssue,
            demand.IssueLinks.Count > 0
                ? demand.IssueLinks.Select(issue => new IssueLinkDto(issue.Key, issue.Url)).ToList().AsReadOnly()
                : (string.IsNullOrWhiteSpace(demand.JiraIssue)
                    ? []
                    : [new IssueLinkDto(demand.JiraIssue, null)]),
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
            demand.NoKpiClassification?.ToString(),
            tradeOffHistoryDtos,
            kpiLinkDtos,
            demandMeasurementDtos,
            demand.CreatedAt,
            demand.UpdatedAt);
    }

    private static string FormatQuarterLabel(int quarterYear, int quarterNumber)
    {
        if (quarterYear <= 0 || quarterNumber <= 0)
            return Quarter.Create(Quarter.BacklogYear, Quarter.BacklogNumber).Label;

        return Quarter.Create(quarterYear, quarterNumber).Label;
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
            relatedDemand.ItemType.ToString(),
            relatedDemand.ProjectId,
            relatedDemand.ProjectId.HasValue && projectNamesById.TryGetValue(relatedDemand.ProjectId.Value, out var projectName) ? projectName : string.Empty,
            relatedDemand.Title,
            relatedDemand.Quarter.Label,
            relatedDemand.QuarterYear,
            relatedDemand.QuarterNumber,
            relatedDemand.Status.ToString());
    }

    private static (Guid? RoadmapId, string? RoadmapTitle, Guid? EpicId, string? EpicTitle, string? ParentTitle) ResolveHierarchy(
        RoadmapDemand demand,
        IReadOnlyDictionary<Guid, RoadmapDemand> demandsById)
    {
        RoadmapDemand? parent = null;
        if (demand.ParentDemandId.HasValue)
            demandsById.TryGetValue(demand.ParentDemandId.Value, out parent);

        if (demand.ItemType == RoadmapItemType.Roadmap)
        {
            return (demand.Id, demand.Title, null, null, null);
        }

        if (demand.ItemType == RoadmapItemType.Epic)
        {
            return (
                parent?.ItemType == RoadmapItemType.Roadmap ? parent.Id : null,
                parent?.ItemType == RoadmapItemType.Roadmap ? parent.Title : null,
                demand.Id,
                demand.Title,
                parent?.Title);
        }

        RoadmapDemand? roadmap = null;
        if (parent?.ParentDemandId.HasValue == true)
            demandsById.TryGetValue(parent.ParentDemandId.Value, out roadmap);

        return (
            roadmap?.ItemType == RoadmapItemType.Roadmap ? roadmap.Id : parent?.ItemType == RoadmapItemType.Roadmap ? parent.Id : null,
            roadmap?.ItemType == RoadmapItemType.Roadmap ? roadmap.Title : parent?.ItemType == RoadmapItemType.Roadmap ? parent.Title : null,
            parent?.ItemType == RoadmapItemType.Epic ? parent.Id : null,
            parent?.ItemType == RoadmapItemType.Epic ? parent.Title : null,
            parent?.Title);
    }
}