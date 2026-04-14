using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetCapacity;

public sealed class GetRoadmapCapacityQueryHandler(
    IRoadmapCapacityRepository capacityRepository,
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository)
    : IRequestHandler<GetRoadmapCapacityQuery, RoadmapCapacityDto>
{
    public async Task<RoadmapCapacityDto> Handle(
        GetRoadmapCapacityQuery request,
        CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        var capacity = await capacityRepository.GetByProjectQuarterAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken);

        var demands = await demandRepository.GetByProjectAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken);

        return MapCapacity(capacity, request.ProjectId, request.QuarterYear, request.QuarterNumber, demands);
    }

    public static RoadmapCapacityDto MapCapacity(
        RoadmapCapacity? capacity,
        Guid projectId,
        int quarterYear,
        int quarterNumber,
        IEnumerable<RoadmapDemand> demands)
    {
        var committedHours = demands
            .Where(d => d.Type != DemandType.Additional)
            .Sum(d => d.Hours ?? 0m);

        var additionalHours = demands
            .Where(d => d.Type == DemandType.Additional)
            .Sum(d => d.Hours ?? 0m);

        var configuredCapacity = capacity?.CapacityHours;
        decimal? remainingHours = configuredCapacity.HasValue
            ? Math.Max(configuredCapacity.Value - committedHours, 0m)
            : null;
        decimal? overCapacityHours = configuredCapacity.HasValue
            ? Math.Max(committedHours - configuredCapacity.Value, 0m)
            : null;

        return new RoadmapCapacityDto(
            capacity?.Id,
            projectId,
            Quarter.Create(quarterYear, quarterNumber).Label,
            quarterYear,
            quarterNumber,
            configuredCapacity,
            capacity?.Observation,
            committedHours,
            additionalHours,
            committedHours + additionalHours,
            remainingHours,
            overCapacityHours);
    }
}