using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
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
        Enum.TryParse<KpiLever>(request.Lever, true, out var lever);
        Enum.TryParse<KpiObjective>(request.Objective, true, out var objective);

        kpi.Update(
            request.Name,
            type,
            lever,
            objective,
            request.Description,
            request.Calculation,
            request.Target,
            request.CurrentValue);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var linkedCount = (await kpiRepository.GetKpiLinksByKpiIdAsync(kpi.Id, cancellationToken)).Count;

        return new KpiDto(
            kpi.Id,
            kpi.ProjectId,
            kpi.Name,
            kpi.Type.ToString(),
            kpi.Lever.ToString(),
            kpi.Objective.ToString(),
            kpi.Description,
            kpi.Calculation,
            kpi.Target,
            kpi.CurrentValue,
            linkedCount,
            kpi.CreatedAt,
            kpi.UpdatedAt);
    }
}
