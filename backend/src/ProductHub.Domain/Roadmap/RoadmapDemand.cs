using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Roadmap;

public sealed class RoadmapDemand : AggregateRoot, IAuditableEntity
{
    private List<RoadmapDemandProduct> _products = [];
    private List<RoadmapDemandProject> _projectLinks = [];
    private IReadOnlyList<RoadmapIssueLink> _issueLinks = [];

    public RoadmapItemType ItemType { get; private set; }
    public Guid? ParentDemandId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? ProjectId { get; private set; }
    public int QuarterYear { get; private set; }
    public int QuarterNumber { get; private set; }
    public DemandStatus Status { get; private set; }
    public DemandType Type { get; private set; }
    public DemandClassification Classification { get; private set; }
    public int SortOrder { get; private set; }
    public string? Observation { get; private set; }
    public DeprioritizationReason? DeprioritizationReason { get; private set; }
    public Guid? ReplacementDemandId { get; private set; }
    public string? JiraIssue { get; private set; }
    public IReadOnlyList<RoadmapIssueLink> IssueLinks => _issueLinks;
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
    public IReadOnlyList<RoadmapDemandProject> ProjectLinks => _projectLinks.AsReadOnly();
    public Quarter Quarter => Quarter.Create(QuarterYear, QuarterNumber);

    private RoadmapDemand() { }

    public static RoadmapDemand Create(
        RoadmapItemType itemType,
        Guid? parentDemandId,
        string title,
        string? description,
        Guid? projectId,
        IEnumerable<Guid>? projectIds,
        int quarterYear,
        int quarterNumber,
        DemandStatus status,
        DemandType type,
        DemandClassification classification,
        IEnumerable<Guid>? productIds,
        int sortOrder = 0,
        string? jiraIssue = null,
        IEnumerable<RoadmapIssueLink>? issueLinks = null,
        decimal? hours = null,
        IEnumerable<string>? customers = null,
        bool isBlocked = false,
        string? blockedReason = null,
        DateOnly? promisedDate = null,
        int? problemClarity = null,
        bool hasNoKpi = false,
        NoKpiClassification? noKpiClassification = null)
    {
        if (problemClarity.HasValue && problemClarity.Value is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(problemClarity), "Problem clarity must be between 0 and 10.");

        ValidateHierarchy(itemType, parentDemandId, projectId);
        var normalizedProjectId = NormalizeProjectId(itemType, projectId);
        var normalizedProjectIds = NormalizeProjectIds(itemType, projectIds);
        var normalizedQuarter = NormalizeQuarter(itemType, quarterYear, quarterNumber);
        Quarter.Create(normalizedQuarter.Year, normalizedQuarter.Number);
        var normalizedHours = itemType == RoadmapItemType.Demand ? hours : null;

        var demand = new RoadmapDemand
        {
            ItemType = itemType,
            ParentDemandId = parentDemandId,
            Title = title,
            Description = description,
            ProjectId = normalizedProjectId,
            QuarterYear = normalizedQuarter.Year,
            QuarterNumber = normalizedQuarter.Number,
            Status = status,
            Type = type,
            Classification = classification,
            SortOrder = sortOrder,
            JiraIssue = jiraIssue,
            _issueLinks = NormalizeIssueLinks(issueLinks),
            Hours = normalizedHours,
            Customers = NormalizeCustomers(customers),
            IsBlocked = isBlocked,
            BlockedReason = isBlocked ? blockedReason : null,
            PromisedDate = promisedDate,
            ProblemClarity = problemClarity,
            HasNoKpi = hasNoKpi,
            NoKpiClassification = NormalizeNoKpiClassification(hasNoKpi, noKpiClassification)
        };
        demand._products = NormalizeProductIds(itemType, productIds)
            .Distinct()
            .Select(id => RoadmapDemandProduct.Create(demand.Id, id))
            .ToList();
        demand._projectLinks = normalizedProjectIds
            .Select(id => RoadmapDemandProject.Create(demand.Id, id))
            .ToList();
        return demand;
    }

    public void Update(
        RoadmapItemType itemType,
        Guid? parentDemandId,
        string title,
        string? description,
        Guid? projectId,
        IEnumerable<Guid>? projectIds,
        int quarterYear,
        int quarterNumber,
        DemandStatus status,
        DemandType type,
        DemandClassification classification,
        int? sortOrder = null,
        string? observation = null,
        DeprioritizationReason? deprioritizationReason = null,
        Guid? replacementDemandId = null,
        string? jiraIssue = null,
        IEnumerable<RoadmapIssueLink>? issueLinks = null,
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
        if (problemClarity.HasValue && problemClarity.Value is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(problemClarity), "Problem clarity must be between 0 and 10.");

        ValidateHierarchy(itemType, parentDemandId, projectId);
        var normalizedProjectId = NormalizeProjectId(itemType, projectId);
        var normalizedProjectIds = NormalizeProjectIds(itemType, projectIds);
        var normalizedQuarter = NormalizeQuarter(itemType, quarterYear, quarterNumber);
        Quarter.Create(normalizedQuarter.Year, normalizedQuarter.Number);
        var normalizedHours = itemType == RoadmapItemType.Demand ? hours : null;

        ItemType = itemType;
        ParentDemandId = parentDemandId;
        Title = title;
        Description = description;
        ProjectId = normalizedProjectId;
        QuarterYear = normalizedQuarter.Year;
        QuarterNumber = normalizedQuarter.Number;
        Status = status;
        Type = type;
        Classification = classification;
        if (sortOrder.HasValue)
            SortOrder = sortOrder.Value;
        Observation = observation;
        DeprioritizationReason = NormalizeDeprioritizationReason(status, deprioritizationReason);
        ReplacementDemandId = status == DemandStatus.Deprioritized ? replacementDemandId : null;
        JiraIssue = jiraIssue;
        _issueLinks = NormalizeIssueLinks(issueLinks);
        Hours = normalizedHours;
        Customers = NormalizeCustomers(customers);
        IsBlocked = isBlocked;
        BlockedReason = isBlocked ? blockedReason : null;
        PromisedDate = promisedDate;
        DeliveryDate = deliveryDate;
        ProblemClarity = problemClarity;
        HasNoKpi = hasNoKpi;
        NoKpiClassification = NormalizeNoKpiClassification(hasNoKpi, noKpiClassification);
    }

    public void ReplaceProducts(IEnumerable<Guid>? productIds)
    {
        _products = NormalizeProductIds(ItemType, productIds)
            .Distinct()
            .Select(id => RoadmapDemandProduct.Create(Id, id))
            .ToList();
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

    private static DeprioritizationReason? NormalizeDeprioritizationReason(
        DemandStatus status,
        DeprioritizationReason? deprioritizationReason)
    {
        if (status != DemandStatus.Deprioritized)
            return null;

        if (!deprioritizationReason.HasValue)
            throw new ArgumentException("Deprioritization reason is required when the demand is deprioritized.", nameof(deprioritizationReason));

        return deprioritizationReason.Value;
    }

    private static IReadOnlyList<string> NormalizeCustomers(IEnumerable<string>? customers) =>
        customers?
            .Select(customer => customer.Trim())
            .Where(customer => !string.IsNullOrWhiteSpace(customer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    private static IReadOnlyList<RoadmapIssueLink> NormalizeIssueLinks(IEnumerable<RoadmapIssueLink>? issueLinks) =>
        issueLinks?
            .Where(issue => !string.IsNullOrWhiteSpace(issue.Key) && !string.IsNullOrWhiteSpace(issue.Url))
            .GroupBy(issue => issue.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList()
        ?? [];

    private static IReadOnlyList<Guid> NormalizeProductIds(RoadmapItemType itemType, IEnumerable<Guid>? productIds) =>
        itemType == RoadmapItemType.Demand
            ? (productIds ?? []).Where(id => id != Guid.Empty).ToList()
            : [];

    private static IReadOnlyList<Guid> NormalizeProjectIds(RoadmapItemType itemType, IEnumerable<Guid>? projectIds) =>
        itemType == RoadmapItemType.Demand
            ? []
            : (projectIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();

    private static Guid? NormalizeProjectId(RoadmapItemType itemType, Guid? projectId) =>
        itemType == RoadmapItemType.Demand && projectId.HasValue && projectId.Value != Guid.Empty
            ? projectId.Value
            : null;

    private static (int Year, int Number) NormalizeQuarter(RoadmapItemType itemType, int quarterYear, int quarterNumber) =>
        itemType == RoadmapItemType.Demand
            ? (quarterYear, quarterNumber)
            : (Quarter.BacklogYear, Quarter.BacklogNumber);

    private static void ValidateHierarchy(RoadmapItemType itemType, Guid? parentDemandId, Guid? projectId)
    {
        if (itemType == RoadmapItemType.Roadmap && parentDemandId.HasValue)
            throw new ArgumentException("Roadmap items cannot have a parent.", nameof(parentDemandId));

        if (itemType != RoadmapItemType.Roadmap && !parentDemandId.HasValue)
            throw new ArgumentException("Epic and demand items require a parent.", nameof(parentDemandId));

        if (itemType == RoadmapItemType.Demand && (!projectId.HasValue || projectId.Value == Guid.Empty))
            throw new ArgumentException("Demand items require a project.", nameof(projectId));
    }
}
