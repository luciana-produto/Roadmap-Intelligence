using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateKpi;

public sealed class CreateKpiCommandHandler(
    IKpiRepository kpiRepository,
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateKpiCommand, KpiDto>
{
    public async Task<KpiDto> Handle(
        CreateKpiCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        Enum.TryParse<KpiType>(request.Type, true, out var type);
        Enum.TryParse<KpiLever>(request.Lever, true, out var lever);
        Enum.TryParse<KpiObjective>(request.Objective, true, out var objective);

        var kpi = Kpi.Create(
            request.ProjectId,
            request.Name,
            type,
            lever,
            objective,
            request.Description,
            request.Calculation,
            request.Target,
            request.CurrentValue);

        await kpiRepository.AddAsync(kpi, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
            0,
            kpi.CreatedAt,
            kpi.UpdatedAt);
    }
}
