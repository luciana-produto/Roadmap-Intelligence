using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.BulkMoveDemands;

public sealed class BulkMoveDemandsToQuarterCommandValidator
    : AbstractValidator<BulkMoveDemandsToQuarterCommand>
{
    public BulkMoveDemandsToQuarterCommandValidator()
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

        RuleFor(x => x.DemandIds)
            .NotEmpty()
            .Must(ids => ids.Any(id => id != Guid.Empty))
            .WithMessage("At least one demand is required.");

        RuleFor(x => x)
            .Must(x => beValidQuarter(x.TargetQuarterYear, x.TargetQuarterNumber))
            .WithMessage("Quarter must be between Q1 and Q4, Backlog, or Backlog - Prioritário.");
    }
}
