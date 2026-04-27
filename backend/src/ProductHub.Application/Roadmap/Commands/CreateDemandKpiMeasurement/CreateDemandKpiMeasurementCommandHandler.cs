using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.CreateDemandKpiMeasurement;

public sealed class CreateDemandKpiMeasurementCommandHandler(
    IKpiRepository kpiRepository,
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateDemandKpiMeasurementCommand, KpiMeasurementDto>
{
    public async Task<KpiMeasurementDto> Handle(
        CreateDemandKpiMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        var demand = await demandRepository.GetByIdAsync(request.DemandId, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.DemandId);

        if (demand.ItemType != RoadmapItemType.Epic)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.DemandId), "KPI measurements are only available for epic items.")
            ]);
        }

        var kpi = await kpiRepository.GetByIdAsync(request.KpiId, cancellationToken)
            ?? throw new NotFoundException("Kpi", request.KpiId);

        var links = await kpiRepository.GetKpiLinksByDemandIdsAsync([request.DemandId], cancellationToken);
        if (!links.Any(link => link.KpiId == request.KpiId))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.KpiId), "KPI must be linked to the demand before registering a measurement.")
            ]);
        }

        Enum.TryParse<MeasurementResult>(request.Result, true, out var result);

        var measurement = KpiMeasurement.Create(
            request.KpiId,
            request.DemandId,
            request.MeasuredValue,
            request.MeasurementDate,
            result,
            request.Observation);

        await kpiRepository.AddMeasurementAsync(measurement, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new KpiMeasurementDto(
            measurement.Id,
            measurement.KpiId,
            kpi.Name,
            measurement.DemandId,
            demand.Title,
            measurement.MeasuredValue,
            measurement.MeasurementDate,
            measurement.Result.ToString(),
            measurement.Observation,
            measurement.CreatedAt);
    }
}