using FluentValidation;

namespace ProductHub.Application.Roadmap.Commands.UpdateProject;

public sealed class UpdateRoadmapProjectCommandValidator : AbstractValidator<UpdateRoadmapProjectCommand>
{
    public UpdateRoadmapProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}