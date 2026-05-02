using MediatR;

namespace ProductHub.Application.Roadmap.Commands.DeleteProjectProduct;

public sealed record DeleteRoadmapProductCommand(Guid ProjectId, Guid ProductId) : IRequest;