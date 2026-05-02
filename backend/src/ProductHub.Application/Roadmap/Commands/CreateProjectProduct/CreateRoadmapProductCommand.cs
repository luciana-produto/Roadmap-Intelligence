using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateProjectProduct;

public sealed record CreateRoadmapProductCommand(Guid ProjectId, string Name) : IRequest<RoadmapProductDto>;