namespace ProductHub.Application.Roadmap.DTOs;

public sealed record KpiDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string Type,
    string Category,
    string Indicator,
    string Operation,
    IReadOnlyList<string> AllowedUnits,
    string? Description,
    int LinkedDemandsCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record DemandKpiLinkDto(
    Guid Id,
    Guid DemandId,
    Guid KpiId,
    string KpiName,
    string ImpactType,
    string Unit,
    decimal? EstimatedImpact,
    string ConfidenceLevel,
    string? Observation,
    string? MeasurementReferenceUrl);

public sealed record KpiMeasurementDto(
    Guid Id,
    Guid KpiId,
    string KpiName,
    Guid? DemandId,
    string? DemandTitle,
    decimal MeasuredValue,
    DateOnly MeasurementDate,
    string Result,
    string? Observation,
    DateTime CreatedAt);

public sealed record DemandTradeOffDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string QuarterLabel,
    int QuarterYear,
    int QuarterNumber,
    Guid DeprioritizedDemandId,
    string DeprioritizedDemandTitle,
    Guid? ReplacementDemandId,
    string? ReplacementDemandTitle,
    string Reason,
    string? Observation,
    DateTime CreatedAt);

public sealed record HealthScoreDto(
    Guid ProjectId,
    int? QuarterYear,
    int? QuarterNumber,
    decimal OverallScore,
    decimal KpiCoverageScore,
    decimal ProblemClarityScore,
    decimal DeliveryRateScore,
    decimal TradeOffDocumentationScore,
    decimal DependencyHealthScore,
    int TotalDemands,
    int DemandsWithKpi,
    int DemandsWithProblemClarity,
    int DemandsDone,
    int DemandsDeprioritized,
    int DeprioritizedWithTradeOff,
    int TotalDependencies,
    int BlockedDependencies);

public sealed record DashboardSummaryDto(
    Guid ProjectId,
    HealthScoreDto HealthScore,
    IReadOnlyList<KpiDto> Kpis,
    IReadOnlyList<DemandTradeOffDto> RecentTradeOffs,
    IReadOnlyList<KpiMeasurementDto> RecentMeasurements,
    DemandStatusDistributionDto StatusDistribution);

public sealed record DemandStatusDistributionDto(
    int Backlog,
    int InProgress,
    int Done,
    int Deprioritized,
    int Spillover,
    int Total);
