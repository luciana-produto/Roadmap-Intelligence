using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetCapacity;

public sealed record GetRoadmapCapacityQuery(
    Guid ProjectId,
    int QuarterYear,
    int QuarterNumber)
    : IRequest<RoadmapCapacityDto>;