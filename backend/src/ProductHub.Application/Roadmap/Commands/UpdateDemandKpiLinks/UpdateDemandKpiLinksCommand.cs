using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiLinks;

// A operação/direção do impacto não é mais informada no vínculo: o handler a
// deriva da "Operação" do cadastro do KPI (fonte de verdade).
public sealed record DemandKpiLinkInput(
    Guid KpiId,
    string Unit,
    decimal? EstimatedImpact,
    string ConfidenceLevel,
    string? Observation,
    string? MeasurementReferenceUrl);

public sealed record UpdateDemandKpiLinksCommand(
    Guid DemandId,
    IReadOnlyList<DemandKpiLinkInput> Links) : IRequest<IReadOnlyList<DemandKpiLinkDto>>;
