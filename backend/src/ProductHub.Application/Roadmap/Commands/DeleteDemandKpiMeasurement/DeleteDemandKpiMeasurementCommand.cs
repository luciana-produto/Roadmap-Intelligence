using MediatR;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemandKpiMeasurement;

public sealed record DeleteDemandKpiMeasurementCommand(Guid Id) : IRequest<Unit>;