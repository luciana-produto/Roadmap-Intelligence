using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.UpdateKpi;

public sealed class UpdateKpiCommandValidator : AbstractValidator<UpdateKpiCommand>
{
    public UpdateKpiCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<KpiType>(t, true, out _))
            .WithMessage("Type must be Business, Product, Quality or Usability.");
        RuleFor(x => x.Lever)
            .NotEmpty()
            .Must(l => Enum.TryParse<KpiLever>(l, true, out _))
            .WithMessage("Lever must be Growth, Efficiency or Customer.");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Calculation).MaximumLength(500);
        RuleFor(x => x.Target).GreaterThanOrEqualTo(0).When(x => x.Target.HasValue);
    }
}
