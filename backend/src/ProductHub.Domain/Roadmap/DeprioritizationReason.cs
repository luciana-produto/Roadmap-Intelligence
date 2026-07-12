namespace ProductHub.Domain.Roadmap;

public enum DeprioritizationReason
{
    Strategic = 0,
    MandatoryUrgent = 1,
    LowImpact = 2,
    LackOfCapacity = 3,
    ContextChange = 4,
    Customizacao = 5,
    StrategyChange = 6,
    HigherValuePrioritization = 7,
    LowCustomerDemand = 8,
    LowExpectedReturn = 9,
    BusinessDefinitionDependency = 10,
    AlternativeSolutionAvailable = 11,
    RegulatoryRequirementChanged = 12,
    CustomerWithdrew = 13,
    ReplacedByOtherInitiative = 14,
    UndefinedScope = 15
}
