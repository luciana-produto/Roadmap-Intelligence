using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Application.Roadmap.Mapping;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpdateKpi;

public sealed class UpdateKpiCommandHandler(
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateKpiCommand, KpiDto>
{
    public async Task<KpiDto> Handle(
        UpdateKpiCommand request,
        CancellationToken cancellationToken)
    {
        var kpi = await kpiRepository.GetByIdTrackedAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kpi", request.Id);

        Enum.TryParse<KpiType>(request.Type, true, out var type);
        Enum.TryParse<KpiCategory>(request.Category, true, out var category);
        Enum.TryParse<KpiIndicator>(request.Indicator, true, out var indicator);
        Enum.TryParse<KpiOperation>(request.Operation, true, out var operation);
        var allowedUnits = KpiMapping.ParseUnits(request.AllowedUnits);

        kpi.Update(
            request.Name,
            type,
            category,
            indicator,
            operation,
            allowedUnits,
            request.Description);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var linkedCount = (await kpiRepository.GetKpiLinksByKpiIdAsync(kpi.Id, cancellationToken)).Count;

        return KpiMapping.ToDto(kpi, linkedCount);
    }
}
