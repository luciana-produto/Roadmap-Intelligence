using FluentValidation;

namespace ProductHub.Application.Roadmap.Commands.CreateProject;

public sealed class CreateRoadmapProjectCommandValidator : AbstractValidator<CreateRoadmapProjectCommand>
{
    public CreateRoadmapProjectCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}