using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.DTOs;

public static class RoadmapProjectDtoMapper
{
    public static RoadmapProjectDto Map(RoadmapProject project) =>
        new(
            project.Id,
            project.Name,
            project.Slug,
            project.Products
                .OrderBy(product => product.Name)
                .Select(product => new RoadmapProductDto(product.Id, product.Name, product.ProjectId))
                .ToList()
                .AsReadOnly());
}