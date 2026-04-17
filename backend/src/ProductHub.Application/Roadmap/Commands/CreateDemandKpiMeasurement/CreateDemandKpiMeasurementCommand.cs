using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateDemandKpiMeasurement;

public sealed record CreateDemandKpiMeasurementCommand(
    Guid DemandId,
    Guid KpiId,
    decimal MeasuredValue,
    DateOnly MeasurementDate,
    string Result,
    string? Observation = null) : IRequest<KpiMeasurementDto>;