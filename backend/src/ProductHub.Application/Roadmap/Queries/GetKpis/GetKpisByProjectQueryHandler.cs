using MediatR;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetKpis;

public sealed class GetKpisQueryHandler(IKpiRepository kpiRepository)
    : IRequestHandler<GetKpisQuery, IEnumerable<KpiDto>>
{
    public async Task<IEnumerable<KpiDto>> Handle(
        GetKpisQuery request,
        CancellationToken cancellationToken)
    {
        var kpis = (await kpiRepository.GetAllAsync(cancellationToken))
            .OrderBy(kpi => kpi.Name)
            .ToArray();
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
            kpi.Objective.ToString(),
            kpi.Description,
            kpi.Calculation,
            kpi.Target,
            kpi.CurrentValue,
            linkCountByKpi.GetValueOrDefault(kpi.Id, 0),
            kpi.CreatedAt,
            kpi.UpdatedAt));
    }
}
