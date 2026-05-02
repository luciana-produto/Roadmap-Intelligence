using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateProject;

public sealed record CreateRoadmapProjectCommand(string Name) : IRequest<RoadmapProjectDto>;