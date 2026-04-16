using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiLinks;

public sealed record DemandKpiLinkInput(
    Guid KpiId,
    string ImpactType,
    decimal? EstimatedImpact,
    string ConfidenceLevel);

public sealed record UpdateDemandKpiLinksCommand(
    Guid DemandId,
    IReadOnlyList<DemandKpiLinkInput> Links) : IRequest<IReadOnlyList<DemandKpiLinkDto>>;
