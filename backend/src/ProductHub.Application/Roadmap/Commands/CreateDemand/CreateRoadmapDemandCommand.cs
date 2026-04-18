using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateDemand;

public sealed record CreateRoadmapDemandCommand(
    string Title,
    string? Description,
    Guid ProjectId,
    int QuarterYear,
    int QuarterNumber,
    string Type,
    string Classification,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid>? DependencyDemandIds = null,
    Guid? ReplacementDemandId = null,
    string? JiraIssue = null,
    decimal? Hours = null,
    DateOnly? PromisedDate = null,
    IReadOnlyList<string>? Customers = null,
    bool IsBlocked = false,
    string? BlockedReason = null,
    int? ProblemClarity = null,
    bool HasNoKpi = false,
    string? NoKpiClassification = null) : IRequest<RoadmapDemandDto>;
