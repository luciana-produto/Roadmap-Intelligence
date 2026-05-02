using FluentValidation;

namespace ProductHub.Application.Roadmap.Commands.CreateProjectProduct;

public sealed class CreateRoadmapProductCommandValidator : AbstractValidator<CreateRoadmapProductCommand>
{
    public CreateRoadmapProductCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}