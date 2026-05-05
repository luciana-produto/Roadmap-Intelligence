using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.CreateDemand;

public sealed class CreateRoadmapDemandCommandValidator
    : AbstractValidator<CreateRoadmapDemandCommand>
{
    public CreateRoadmapDemandCommandValidator()
    {
        static bool beValidQuarter(int year, int number)
        {
            try
            {
                Quarter.Create(year, number);
                return true;
            }
            catch
            {
                return false;
            }
        }

        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ItemType)
            .NotEmpty()
            .Must(value => Enum.TryParse<RoadmapItemType>(value, true, out _))
            .WithMessage("Item type must be Roadmap, Epic or Demand.");
        RuleFor(x => x.ParentDemandId)
            .Null().When(x => x.ItemType == nameof(RoadmapItemType.Roadmap))
            .WithMessage("Roadmap items cannot have a parent.");
        RuleFor(x => x.ParentDemandId)
            .NotNull().When(x => x.ItemType != nameof(RoadmapItemType.Roadmap))
            .WithMessage("Epic and demand items require a parent.");
        RuleFor(x => x.ProjectId)
            .NotEmpty().When(x => x.ItemType == nameof(RoadmapItemType.Demand))
            .WithMessage("A demand requires a project.");
        RuleFor(x => x.ProjectIds)
            .Must(ids => ids == null || ids.Where(id => id != Guid.Empty).Distinct().Count() == ids.Count)
            .WithMessage("Project associations must be unique.");
        RuleFor(x => x)
            .Must(x => beValidQuarter(x.QuarterYear, x.QuarterNumber))
            .WithMessage("Quarter must be between Q1 and Q4, Backlog, or Backlog - Prioritário.");
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(value => Enum.TryParse<DemandStatus>(value, true, out _))
            .WithMessage("Status must be Backlog, InProgress, Done or Deprioritized.");
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<DemandType>(t, true, out _))
            .WithMessage("Type must be Planned, Spillover, Unplanned or Additional.");
        RuleFor(x => x.Classification)
            .NotEmpty()
            .Must(c => Enum.TryParse<DemandClassification>(c, true, out _))
            .WithMessage("Invalid classification value.");
        RuleFor(x => x.ProductIds)
            .NotEmpty().When(x => x.ItemType == nameof(RoadmapItemType.Demand)).WithMessage("At least one product is required.")
            .Must(ids => ids.Count > 0).When(x => x.ItemType == nameof(RoadmapItemType.Demand)).WithMessage("At least one product is required.");
        RuleFor(x => x.DependencyDemandIds)
            .Must(ids => ids == null || ids.Where(id => id != Guid.Empty).Distinct().Count() == ids.Count)
            .WithMessage("Dependency demands must be unique.");
        RuleFor(x => x.ReplacementDemandId)
            .NotEmpty().When(x => x.ReplacementDemandId.HasValue)
            .WithMessage("Replacement demand must be valid.");
        RuleFor(x => x.JiraIssue).MaximumLength(100);
                RuleForEach(x => x.IssueLinks).ChildRules(link =>
                {
                    link.RuleFor(x => x.Key)
                        .NotEmpty().WithMessage("Issue key is required.")
                        .MaximumLength(100).WithMessage("Issue key must have a maximum length of 100 characters.");
                    link.RuleFor(x => x.Url)
                        .NotEmpty().WithMessage("Issue link is required.")
                        .MaximumLength(1000).WithMessage("Issue link must have a maximum length of 1000 characters.")
                        .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _)).WithMessage("Issue link must be a valid absolute URL.");
                });
        RuleFor(x => x.Hours).GreaterThan(0).When(x => x.Hours.HasValue);
        RuleFor(x => x.PromisedDate)
            .Must((command, promisedDate) => !promisedDate.HasValue || command.ItemType != nameof(RoadmapItemType.Demand) || command.QuarterNumber > 0)
            .WithMessage("Promised date requires a prioritized quarter.");
        RuleFor(x => x.Customers)
            .Must(customers => customers == null || string.Join(", ", customers).Length <= 500)
            .WithMessage("Customers must have a maximum combined length of 500 characters.");
        RuleFor(x => x.BlockedReason)
            .NotEmpty().WithMessage("Blocked reason is required when demand is blocked.")
            .When(x => x.IsBlocked);
        RuleFor(x => x.BlockedReason).MaximumLength(500);
        RuleFor(x => x.ProblemClarity)
            .InclusiveBetween(0, 10).When(x => x.ProblemClarity.HasValue)
            .WithMessage("Problem clarity must be between 0 and 10.");
        RuleFor(x => x.NoKpiClassification)
            .NotEmpty().WithMessage("No KPI classification is required when the demand is marked without KPI.")
            .When(x => x.HasNoKpi);
        RuleFor(x => x.NoKpiClassification)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.TryParse<NoKpiClassification>(value, true, out _))
            .WithMessage("No KPI classification must be Relationship, Mandatory or Technical.");
        RuleFor(x => x.NoKpiClassification)
            .Empty().WithMessage("No KPI classification must be empty when the demand has KPI.")
            .When(x => !x.HasNoKpi);
    }
}
