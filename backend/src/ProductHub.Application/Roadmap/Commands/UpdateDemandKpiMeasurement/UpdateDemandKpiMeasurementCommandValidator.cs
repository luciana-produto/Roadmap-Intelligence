using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiMeasurement;

public sealed class UpdateDemandKpiMeasurementCommandValidator : AbstractValidator<UpdateDemandKpiMeasurementCommand>
{
    public UpdateDemandKpiMeasurementCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.MeasuredValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Result)
            .NotEmpty()
            .Must(result => Enum.TryParse<MeasurementResult>(result, true, out _))
            .WithMessage("Result must be Positive, Negative or Neutral.");
        RuleFor(x => x.Observation).MaximumLength(2000);
    }
}