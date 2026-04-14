using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetProjects;

public sealed record GetProjectsQuery : IRequest<IEnumerable<RoadmapProjectDto>>;
