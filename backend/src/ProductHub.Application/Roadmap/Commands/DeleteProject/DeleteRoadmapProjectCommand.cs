using MediatR;

namespace ProductHub.Application.Roadmap.Commands.DeleteProject;

public sealed record DeleteRoadmapProjectCommand(Guid Id) : IRequest;