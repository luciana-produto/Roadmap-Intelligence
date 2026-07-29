using MediatR;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Application.Roadmap.Mapping;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateKpi;

public sealed class CreateKpiCommandHandler(
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateKpiCommand, KpiDto>
{
    public async Task<KpiDto> Handle(
        CreateKpiCommand request,
        CancellationToken cancellationToken)
    {
        Enum.TryParse<KpiType>(request.Type, true, out var type);
        Enum.TryParse<KpiCategory>(request.Category, true, out var category);
        Enum.TryParse<KpiIndicator>(request.Indicator, true, out var indicator);
        Enum.TryParse<KpiOperation>(request.Operation, true, out var operation);
        var allowedUnits = KpiMapping.ParseUnits(request.AllowedUnits);

        var kpi = Kpi.Create(
            null,
            request.Name,
            type,
            category,
            indicator,
            operation,
            allowedUnits,
            request.Description);

        await kpiRepository.AddAsync(kpi, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return KpiMapping.ToDto(kpi, 0);
    }
}
