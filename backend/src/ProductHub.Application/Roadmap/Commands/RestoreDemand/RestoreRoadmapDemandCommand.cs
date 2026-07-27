using MediatR;

namespace ProductHub.Application.Roadmap.Commands.RestoreDemand;

public sealed record RestoreRoadmapDemandCommand(Guid Id) : IRequest;
