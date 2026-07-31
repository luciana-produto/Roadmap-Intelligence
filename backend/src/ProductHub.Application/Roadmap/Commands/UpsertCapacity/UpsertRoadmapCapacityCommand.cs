using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpsertCapacity;

public sealed record UpsertRoadmapCapacityCommand(
    Guid ProjectId,
    int QuarterYear,
    int QuarterNumber,
    decimal CapacityHours,
    string? Observation = null,
    decimal TechnicalDebtPercent = 20m) : IRequest<RoadmapCapacityDto>;