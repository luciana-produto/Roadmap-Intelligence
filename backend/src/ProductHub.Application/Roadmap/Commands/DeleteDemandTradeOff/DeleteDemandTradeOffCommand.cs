using MediatR;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemandTradeOff;

public sealed record DeleteDemandTradeOffCommand(Guid Id) : IRequest<Unit>;