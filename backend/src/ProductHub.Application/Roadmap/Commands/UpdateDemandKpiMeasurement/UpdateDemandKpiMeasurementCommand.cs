using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiMeasurement;

public sealed record UpdateDemandKpiMeasurementCommand(
    Guid Id,
    decimal MeasuredValue,
    DateOnly MeasurementDate,
    string Result,
    string? Observation = null) : IRequest<KpiMeasurementDto>;