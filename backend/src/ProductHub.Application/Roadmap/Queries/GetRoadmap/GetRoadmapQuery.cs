using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetRoadmap;

public sealed record GetDemandsQuery(
    Guid ProjectId,
    int? QuarterYear = null,
    int? QuarterNumber = null)
    : IRequest<IEnumerable<RoadmapDemandDto>>;
