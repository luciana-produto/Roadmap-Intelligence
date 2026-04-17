using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.CreateDemandKpiMeasurement;

public sealed class CreateDemandKpiMeasurementCommandValidator : AbstractValidator<CreateDemandKpiMeasurementCommand>
{
    public CreateDemandKpiMeasurementCommandValidator()
    {
        RuleFor(x => x.DemandId).NotEmpty();
        RuleFor(x => x.KpiId).NotEmpty();
        RuleFor(x => x.MeasuredValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Result)
            .NotEmpty()
            .Must(result => Enum.TryParse<MeasurementResult>(result, true, out _))
            .WithMessage("Result must be Positive, Negative or Neutral.");
        RuleFor(x => x.Observation).MaximumLength(2000);
    }
}