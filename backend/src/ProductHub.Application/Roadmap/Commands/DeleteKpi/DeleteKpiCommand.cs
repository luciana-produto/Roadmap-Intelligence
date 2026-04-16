using MediatR;

namespace ProductHub.Application.Roadmap.Commands.DeleteKpi;

public sealed record DeleteKpiCommand(Guid Id) : IRequest;
