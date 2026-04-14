using MediatR;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemand;

public sealed record DeleteRoadmapDemandCommand(Guid Id) : IRequest;
