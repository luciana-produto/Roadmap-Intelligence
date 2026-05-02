using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpdateProjectProduct;

public sealed record UpdateRoadmapProductCommand(Guid ProjectId, Guid ProductId, string Name) : IRequest<RoadmapProductDto>;