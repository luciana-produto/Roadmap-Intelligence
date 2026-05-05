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
            link.RuleFor(l => l.Observation)
                .MaximumLength(1000);
            link.RuleFor(l => l.MeasurementReferenceUrl)
                .MaximumLength(2000)
                .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("MeasurementReferenceUrl must be a valid absolute URL.");
        });
        RuleFor(x => x.Links)
            .Must(links => links.Select(l => l.KpiId).Distinct().Count() == links.Count)
            .WithMessage("Each KPI can only be linked once per demand.");
    }
}
