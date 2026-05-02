using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpdateProject;

public sealed record UpdateRoadmapProjectCommand(Guid Id, string Name) : IRequest<RoadmapProjectDto>;