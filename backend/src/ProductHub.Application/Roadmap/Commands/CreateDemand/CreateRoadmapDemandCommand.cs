using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateDemand;

public sealed record CreateRoadmapDemandCommand(
    string ItemType,
    Guid? ParentDemandId,
    string Title,
    string? Description,
    Guid? ProjectId,
    IReadOnlyList<Guid>? ProjectIds,
    int QuarterYear,
    int QuarterNumber,
    string Status,
    string Type,
    string Classification,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid>? DependencyDemandIds = null,
    string? Observation = null,
    IReadOnlyList<IssueLinkInput>? IssueLinks = null,
    string? DeprioritizationReason = null,
    Guid? ReplacementDemandId = null,
    string? JiraIssue = null,
    decimal? Hours = null,
    DateOnly? PromisedDate = null,
    IReadOnlyList<string>? Customers = null,
    bool IsBlocked = false,
    string? BlockedReason = null,
    DateOnly? DeliveryDate = null,
    int? ProblemClarity = null,
    bool HasNoKpi = false,
    string? NoKpiClassification = null) : IRequest<RoadmapDemandDto>;
