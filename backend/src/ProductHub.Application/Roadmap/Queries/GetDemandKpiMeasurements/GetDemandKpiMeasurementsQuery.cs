using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetDemandKpiMeasurements;

public sealed record GetDemandKpiMeasurementsQuery(Guid DemandId) : IRequest<IReadOnlyList<KpiMeasurementDto>>;