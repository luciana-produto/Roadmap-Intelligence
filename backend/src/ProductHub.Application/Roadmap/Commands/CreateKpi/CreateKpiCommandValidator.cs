using FluentValidation;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Commands.CreateKpi;

public sealed class CreateKpiCommandValidator : AbstractValidator<CreateKpiCommand>
{
    public CreateKpiCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<KpiType>(t, true, out _))
            .WithMessage("Tipo deve ser Negócio (Business) ou Produto (Product).");
        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(c => Enum.TryParse<KpiCategory>(c, true, out _))
            .WithMessage("Categoria deve ser Financeiro, Crescimento ou Eficiência.");
        RuleFor(x => x.Indicator)
            .NotEmpty()
            .Must(i => Enum.TryParse<KpiIndicator>(i, true, out _))
            .WithMessage("Indicador inválido.");
        RuleFor(x => x.Operation)
            .NotEmpty()
            .Must(o => Enum.TryParse<KpiOperation>(o, true, out _))
            .WithMessage("Operação deve ser \"Quanto maior melhor\" ou \"Quanto menor melhor\".");
        RuleFor(x => x.AllowedUnits)
            .NotEmpty()
            .WithMessage("Selecione ao menos uma unidade permitida.")
            .Must(units => units != null && units.All(u => Enum.TryParse<KpiUnit>(u, true, out _)))
            .WithMessage("Unidade permitida inválida.");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
