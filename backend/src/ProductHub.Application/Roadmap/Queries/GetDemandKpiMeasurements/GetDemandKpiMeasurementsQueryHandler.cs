using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetDemandKpiMeasurements;

public sealed class GetDemandKpiMeasurementsQueryHandler(
    IKpiRepository kpiRepository,
    IRoadmapDemandRepository demandRepository)
    : IRequestHandler<GetDemandKpiMeasurementsQuery, IReadOnlyList<KpiMeasurementDto>>
{
    public async Task<IReadOnlyList<KpiMeasurementDto>> Handle(
        GetDemandKpiMeasurementsQuery request,
        CancellationToken cancellationToken)
    {
        var demand = await demandRepository.GetByIdAsync(request.DemandId, cancellationToken)
            ?? throw new NotFoundException("RoadmapDemand", request.DemandId);

        var links = await kpiRepository.GetKpiLinksByDemandIdsAsync([request.DemandId], cancellationToken);
        var kpiIds = links.Select(link => link.KpiId).Distinct().ToArray();
        var kpiNamesById = kpiIds.Length > 0
            ? (await kpiRepository.GetByProjectAsync(demand.ProjectId, cancellationToken))
                .Where(kpi => kpiIds.Contains(kpi.Id))
                .ToDictionary(kpi => kpi.Id, kpi => kpi.Name)
            : new Dictionary<Guid, string>();

        var measurements = await kpiRepository.GetMeasurementsByDemandIdAsync(request.DemandId, cancellationToken);

        return measurements
            .Select(measurement => new KpiMeasurementDto(
                measurement.Id,
                measurement.KpiId,
                kpiNamesById.TryGetValue(measurement.KpiId, out var kpiName) ? kpiName : string.Empty,
                measurement.DemandId,
                demand.Title,
                measurement.MeasuredValue,
                measurement.MeasurementDate,
                measurement.Result.ToString(),
                measurement.Observation,
                measurement.CreatedAt))
            .ToList()
            .AsReadOnly();
    }
}