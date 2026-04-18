using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiLinks;

public sealed class UpdateDemandKpiLinksCommandHandler(
    IKpiRepository kpiRepository,
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDemandKpiLinksCommand, IReadOnlyList<DemandKpiLinkDto>>
{
    public async Task<IReadOnlyList<DemandKpiLinkDto>> Handle(
        UpdateDemandKpiLinksCommand request,
        CancellationToken cancellationToken)
    {
        _ = await demandRepository.GetByIdAsync(request.DemandId, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.DemandId);

        var kpiIds = request.Links.Select(l => l.KpiId).Distinct().ToArray();
        var kpis = (await kpiRepository.GetAllAsync(cancellationToken))
            .Where(k => kpiIds.Contains(k.Id))
            .ToDictionary(k => k.Id);

        foreach (var kpiId in kpiIds)
            if (!kpis.ContainsKey(kpiId))
                throw new NotFoundException("Kpi", kpiId);

        var links = request.Links.Select(input =>
        {
            Enum.TryParse<ImpactType>(input.ImpactType, true, out var impactType);
            Enum.TryParse<ConfidenceLevel>(input.ConfidenceLevel, true, out var confidence);
            return DemandKpiLink.FromRepository(
                request.DemandId,
                input.KpiId,
                impactType,
                input.EstimatedImpact,
                confidence,
                input.Observation);
        }).ToList();

        await kpiRepository.ReplaceDemandKpiLinksAsync(request.DemandId, links, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedLinks = await kpiRepository.GetKpiLinksByDemandIdsAsync([request.DemandId], cancellationToken);
        var kpiNamesById = kpis.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);

        return savedLinks.Select(link => new DemandKpiLinkDto(
            link.Id,
            link.DemandId,
            link.KpiId,
            kpiNamesById.GetValueOrDefault(link.KpiId, string.Empty),
            link.ImpactType.ToString(),
            link.EstimatedImpact,
            link.ConfidenceLevel.ToString(),
            link.Observation)).ToList().AsReadOnly();
    }
}
