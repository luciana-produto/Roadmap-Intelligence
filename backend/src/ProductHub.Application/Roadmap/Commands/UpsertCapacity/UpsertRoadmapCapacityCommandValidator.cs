using FluentValidation;

namespace ProductHub.Application.Roadmap.Commands.UpsertCapacity;

public sealed class UpsertRoadmapCapacityCommandValidator : AbstractValidator<UpsertRoadmapCapacityCommand>
{
    public UpsertRoadmapCapacityCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.QuarterYear).InclusiveBetween(2020, 2100);
        RuleFor(x => x.QuarterNumber).InclusiveBetween(1, 4);
        RuleFor(x => x.CapacityHours).GreaterThan(0);
        RuleFor(x => x.Observation).MaximumLength(2000);
    }
}