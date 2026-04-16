using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiLinks;

public sealed class UpdateDemandKpiLinksCommandValidator
    : AbstractValidator<UpdateDemandKpiLinksCommand>
{
    public UpdateDemandKpiLinksCommandValidator()
    {
        RuleFor(x => x.DemandId).NotEmpty();
        RuleFor(x => x.Links).NotNull();
        RuleForEach(x => x.Links).ChildRules(link =>
        {
            link.RuleFor(l => l.KpiId).NotEmpty();
            link.RuleFor(l => l.ImpactType)
                .NotEmpty()
                .Must(t => Enum.TryParse<ImpactType>(t, true, out _))
                .WithMessage("ImpactType must be Increase or Decrease.");
            link.RuleFor(l => l.ConfidenceLevel)
                .NotEmpty()
                .Must(c => Enum.TryParse<ConfidenceLevel>(c, true, out _))
                .WithMessage("ConfidenceLevel must be High, Medium or Low.");
        });
        RuleFor(x => x.Links)
            .Must(links => links.Select(l => l.KpiId).Distinct().Count() == links.Count)
            .WithMessage("Each KPI can only be linked once per demand.");
    }
}
