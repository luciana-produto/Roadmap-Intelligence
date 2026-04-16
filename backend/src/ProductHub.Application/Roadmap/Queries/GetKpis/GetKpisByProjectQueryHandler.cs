using MediatR;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetKpis;

public sealed class GetKpisByProjectQueryHandler(IKpiRepository kpiRepository)
    : IRequestHandler<GetKpisByProjectQuery, IEnumerable<KpiDto>>
{
    public async Task<IEnumerable<KpiDto>> Handle(
        GetKpisByProjectQuery request,
        CancellationToken cancellationToken)
    {
        var kpis = await kpiRepository.GetByProjectAsync(request.ProjectId, cancellationToken);
        var kpiIds = kpis.Select(k => k.Id).ToArray();

        var allLinks = new List<Domain.Roadmap.DemandKpiLink>();
        foreach (var kpiId in kpiIds)
        {
            var links = await kpiRepository.GetKpiLinksByKpiIdAsync(kpiId, cancellationToken);
            allLinks.AddRange(links);
        }

        var linkCountByKpi = allLinks
            .GroupBy(l => l.KpiId)
            .ToDictionary(g => g.Key, g => g.Count());

        return kpis.Select(kpi => new KpiDto(
            kpi.Id,
            kpi.ProjectId,
            kpi.Name,
            kpi.Type.ToString(),
            kpi.Lever.ToString(),
            kpi.Description,
            kpi.Calculation,
            kpi.Target,
            kpi.CurrentValue,
            linkCountByKpi.GetValueOrDefault(kpi.Id, 0),
            kpi.CreatedAt,
            kpi.UpdatedAt));
    }
}
