namespace ProductHub.Domain.Roadmap;

public enum SpilloverReason
{
    ScopeChange = 0,
    PriorityChangeNoTradeOff = 1,
    ExternalDependency = 2,
    TechnicalBlock = 3,
    IncorrectEstimate = 4,
    InsufficientCapacity = 5,
    QualityIssues = 6
}
