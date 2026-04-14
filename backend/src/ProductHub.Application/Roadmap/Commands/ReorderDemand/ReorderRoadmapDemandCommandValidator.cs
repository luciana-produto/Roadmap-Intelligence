using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.ReorderDemand;

public sealed class ReorderRoadmapDemandCommandValidator
    : AbstractValidator<ReorderRoadmapDemandCommand>
{
    public ReorderRoadmapDemandCommandValidator()
    {
        RuleFor(x => x.DemandId).NotEmpty();
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(status => Enum.TryParse<DemandStatus>(status, true, out _))
            .WithMessage("Status must be Backlog, InProgress, Done or Deprioritized.");
        RuleFor(x => x.OrderedDemandIds)
            .NotEmpty()
            .Must(ids => ids.Count > 0)
            .WithMessage("OrderedDemandIds must include the current demand scope.");
    }
}
