using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.DTOs;

internal static class RoadmapDemandDtoMapper
{
    public static RoadmapDemandDto Map(
        RoadmapDemand demand,
        IReadOnlyDictionary<Guid, string> productNamesById,
        IReadOnlyDictionary<Guid, RoadmapDemand> demandsById,
        IReadOnlyDictionary<Guid, string> projectNamesById,
        IEnumerable<RoadmapDemandDependency> dependencyLinks)
    {
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
            demand.DeliveryDate,
            demand.CreatedAt,
            demand.UpdatedAt);
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
            relatedDemand.Status.ToString());
    }
}