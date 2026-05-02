using FluentValidation;

namespace ProductHub.Application.Roadmap.Commands.UpdateProjectProduct;

public sealed class UpdateRoadmapProductCommandValidator : AbstractValidator<UpdateRoadmapProductCommand>
{
    public UpdateRoadmapProductCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}