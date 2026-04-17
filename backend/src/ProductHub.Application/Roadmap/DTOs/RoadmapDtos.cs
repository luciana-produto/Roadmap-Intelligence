namespace ProductHub.Application.Roadmap.DTOs;

public sealed record DemandProductDto(Guid ProductId, string Name);

public sealed record DemandDependencyDto(
    Guid DemandId,
    Guid ProjectId,
    string ProjectName,
    string Title,
    string QuarterLabel,
    int QuarterYear,
    int QuarterNumber,
    string Status);

public sealed record DemandDependencyOptionDto(
    Guid DemandId,
    Guid ProjectId,
    string ProjectName,
    string Title,
    string QuarterLabel,
    int QuarterYear,
    int QuarterNumber,
    string Status);

public sealed record RoadmapProductDto(Guid Id, string Name, Guid ProjectId);

public sealed record RoadmapProjectDto(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<RoadmapProductDto> Products);

public sealed record RoadmapDemandDto(
    Guid Id,
    string Title,
    string? Description,
    Guid ProjectId,
    string QuarterLabel,
    int QuarterYear,
    int QuarterNumber,
    int SortOrder,
    string Status,
    string Type,
    string Classification,
    IReadOnlyList<DemandProductDto> Products,
    string? Observation,
    string? JiraIssue,
    decimal? Hours,
    IReadOnlyList<string> Customers,
    bool IsBlocked,
    string? BlockedReason,
    IReadOnlyList<DemandDependencyDto> DependsOn,
    IReadOnlyList<DemandDependencyDto> DependedOnBy,
    DateOnly? PromisedDate,
    DateOnly? EffectivePromisedDate,
    DateOnly? DeliveryDate,
    bool IsOverdue,
    bool IsDeliveredLate,
    int? ProblemClarity,
    bool HasNoKpi,
    IReadOnlyList<DemandKpiLinkDto> KpiLinks,
    IReadOnlyList<KpiMeasurementDto> KpiMeasurements,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record RoadmapCapacityDto(
    Guid? Id,
    Guid ProjectId,
    string QuarterLabel,
    int QuarterYear,
    int QuarterNumber,
    decimal? CapacityHours,
    string? Observation,
    decimal CommittedHours,
    decimal AdditionalHours,
    decimal TotalDemandHours,
    decimal? RemainingHours,
    decimal? OverCapacityHours);
