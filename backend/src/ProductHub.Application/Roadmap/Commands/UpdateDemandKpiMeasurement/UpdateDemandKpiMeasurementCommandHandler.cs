using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpdateDemandKpiMeasurement;

public sealed class UpdateDemandKpiMeasurementCommandHandler(
    IKpiRepository kpiRepository,
    IRoadmapDemandRepository demandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDemandKpiMeasurementCommand, KpiMeasurementDto>
{
    public async Task<KpiMeasurementDto> Handle(
        UpdateDemandKpiMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        var measurement = await kpiRepository.GetMeasurementByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KpiMeasurement", request.Id);

        var demand = measurement.DemandId.HasValue
            ? await demandRepository.GetByIdAsync(measurement.DemandId.Value, cancellationToken)
            : null;

        if (demand is not null && demand.ItemType != RoadmapItemType.Epic)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(request.Id), "KPI measurements are only available for epic items.")
            ]);
        }

        var kpi = await kpiRepository.GetByIdAsync(measurement.KpiId, cancellationToken)
            ?? throw new NotFoundException("Kpi", measurement.KpiId);

        Enum.TryParse<MeasurementResult>(request.Result, true, out var result);
        measurement.Update(request.MeasuredValue, request.MeasurementDate, result, request.Observation);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new KpiMeasurementDto(
            measurement.Id,
            measurement.KpiId,
            kpi.Name,
            measurement.DemandId,
            demand?.Title,
            measurement.MeasuredValue,
            measurement.MeasurementDate,
            measurement.Result.ToString(),
            measurement.Observation,
            measurement.CreatedAt);
    }
}