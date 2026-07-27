using MediatR;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetDeletedDemands;

public sealed class GetDeletedDemandsQueryHandler(
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository)
    : IRequestHandler<GetDeletedDemandsQuery, IReadOnlyList<DeletedDemandDto>>
{
    public async Task<IReadOnlyList<DeletedDemandDto>> Handle(
        GetDeletedDemandsQuery request,
        CancellationToken cancellationToken)
    {
        var deleted = await demandRepository.GetDeletedAsync(cancellationToken);
        var projects = await projectRepository.GetAllAsync(cancellationToken);
        var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);

        return deleted.Select(d =>
        {
            var effectiveProjectId = d.ProjectId
                ?? (d.IsSimple ? d.ProjectLinks.FirstOrDefault()?.ProjectId : null);

            var projectName = effectiveProjectId.HasValue
                && projectNames.TryGetValue(effectiveProjectId.Value, out var name)
                    ? name
                    : null;

            return new DeletedDemandDto(
                d.Id,
                d.ItemType.ToString(),
                d.Title,
                d.QuarterYear,
                d.QuarterNumber,
                effectiveProjectId,
                projectName,
                d.ParentDemandId,
                d.DeletedAt,
                d.DeletedByEmail);
        }).ToList();
    }
}
