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
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x)
            .Must(x => beValidQuarter(x.QuarterYear, x.QuarterNumber))
            .WithMessage("Quarter must be between Q1 and Q4, or Backlog.");
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<DemandType>(t, true, out _))
            .WithMessage("Type must be Planned, Spillover, Unplanned or Additional.");
        RuleFor(x => x.Classification)
            .NotEmpty()
            .Must(c => Enum.TryParse<DemandClassification>(c, true, out _))
            .WithMessage("Invalid classification value.");
        RuleFor(x => x.ProductIds)
            .NotEmpty().WithMessage("At least one product is required.")
            .Must(ids => ids.Count > 0).WithMessage("At least one product is required.");
        RuleFor(x => x.DependencyDemandIds)
            .Must(ids => ids == null || ids.Where(id => id != Guid.Empty).Distinct().Count() == ids.Count)
            .WithMessage("Dependency demands must be unique.");
        RuleFor(x => x.JiraIssue).MaximumLength(100);
        RuleFor(x => x.Hours).GreaterThan(0).When(x => x.Hours.HasValue);
        RuleFor(x => x.Customers)
            .Must(customers => customers == null || string.Join(", ", customers).Length <= 500)
            .WithMessage("Customers must have a maximum combined length of 500 characters.");
        RuleFor(x => x.BlockedReason)
            .NotEmpty().WithMessage("Blocked reason is required when demand is blocked.")
            .When(x => x.IsBlocked);
        RuleFor(x => x.BlockedReason).MaximumLength(500);
    }
}
