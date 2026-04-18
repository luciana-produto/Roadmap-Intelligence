using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapDemand : AggregateRoot, IAuditableEntity
{
    private List<RoadmapDemandProduct> _products = [];

    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid ProjectId { get; private set; }
    public int QuarterYear { get; private set; }
    public int QuarterNumber { get; private set; }
    public DemandStatus Status { get; private set; }
    public DemandType Type { get; private set; }
    public DemandClassification Classification { get; private set; }
    public int SortOrder { get; private set; }
    public string? Observation { get; private set; }
    public string? JiraIssue { get; private set; }
    public decimal? Hours { get; private set; }
    public IReadOnlyList<string> Customers { get; private set; } = [];
    public bool IsBlocked { get; private set; }
    public string? BlockedReason { get; private set; }
    public DateOnly? PromisedDate { get; private set; }
    public DateOnly? DeliveryDate { get; private set; }
    public int? ProblemClarity { get; private set; }
    public bool HasNoKpi { get; private set; }
    public NoKpiClassification? NoKpiClassification { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyList<RoadmapDemandProduct> Products => _products.AsReadOnly();
    public Quarter Quarter => Quarter.Create(QuarterYear, QuarterNumber);

    private RoadmapDemand() { }

    public static RoadmapDemand Create(
        string title,
        string? description,
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        DemandType type,
        DemandClassification classification,
        IEnumerable<Guid> productIds,
        int sortOrder = 0,
        string? jiraIssue = null,
        decimal? hours = null,
        IEnumerable<string>? customers = null,
        bool isBlocked = false,
        string? blockedReason = null,
        DateOnly? promisedDate = null,
        int? problemClarity = null,
        bool hasNoKpi = false,
        NoKpiClassification? noKpiClassification = null)
    {
        Quarter.Create(quarterYear, quarterNumber);

        if (problemClarity.HasValue && problemClarity.Value is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(problemClarity), "Problem clarity must be between 0 and 10.");

        var demand = new RoadmapDemand
        {
            Title = title,
            Description = description,
            ProjectId = projectId,
            QuarterYear = quarterYear,
            QuarterNumber = quarterNumber,
            Status = DemandStatus.Backlog,
            Type = type,
            Classification = classification,
            SortOrder = sortOrder,
            JiraIssue = jiraIssue,
            Hours = hours,
            Customers = NormalizeCustomers(customers),
            IsBlocked = isBlocked,
            BlockedReason = isBlocked ? blockedReason : null,
            PromisedDate = promisedDate,
            ProblemClarity = problemClarity,
            HasNoKpi = hasNoKpi,
            NoKpiClassification = NormalizeNoKpiClassification(hasNoKpi, noKpiClassification)
        };
        demand._products = productIds
            .Distinct()
            .Select(id => RoadmapDemandProduct.Create(demand.Id, id))
            .ToList();
        return demand;
    }

    public void Update(
        string title,
        string? description,
        int quarterYear,
        int quarterNumber,
        DemandStatus status,
        DemandType type,
        DemandClassification classification,
        int? sortOrder = null,
        string? observation = null,
        string? jiraIssue = null,
        decimal? hours = null,
        IEnumerable<string>? customers = null,
        bool isBlocked = false,
        string? blockedReason = null,
        DateOnly? promisedDate = null,
        DateOnly? deliveryDate = null,
        int? problemClarity = null,
        bool hasNoKpi = false,
        NoKpiClassification? noKpiClassification = null)
    {
        Quarter.Create(quarterYear, quarterNumber);

        if (problemClarity.HasValue && problemClarity.Value is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(problemClarity), "Problem clarity must be between 0 and 10.");

        Title = title;
        Description = description;
        QuarterYear = quarterYear;
        QuarterNumber = quarterNumber;
        Status = status;
        Type = type;
        Classification = classification;
        if (sortOrder.HasValue)
            SortOrder = sortOrder.Value;
        Observation = observation;
        JiraIssue = jiraIssue;
        Hours = hours;
        Customers = NormalizeCustomers(customers);
        IsBlocked = isBlocked;
        BlockedReason = isBlocked ? blockedReason : null;
        PromisedDate = promisedDate;
        DeliveryDate = deliveryDate;
        ProblemClarity = problemClarity;
        HasNoKpi = hasNoKpi;
        NoKpiClassification = NormalizeNoKpiClassification(hasNoKpi, noKpiClassification);
    }

    public void SetSortOrder(int sortOrder) =>
        SortOrder = sortOrder;

    public void SetStatus(DemandStatus status) =>
        Status = status;

    private static NoKpiClassification? NormalizeNoKpiClassification(
        bool hasNoKpi,
        NoKpiClassification? noKpiClassification)
    {
        if (!hasNoKpi)
            return null;

        if (!noKpiClassification.HasValue)
            throw new ArgumentException("No KPI classification is required when the demand has no KPI.", nameof(noKpiClassification));

        return noKpiClassification.Value;
    }

    private static IReadOnlyList<string> NormalizeCustomers(IEnumerable<string>? customers) =>
        customers?
            .Select(customer => customer.Trim())
            .Where(customer => !string.IsNullOrWhiteSpace(customer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];
}
