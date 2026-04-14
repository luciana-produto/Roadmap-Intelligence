using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Application.Roadmap.Queries.GetCapacity;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.UpsertCapacity;

public sealed class UpsertRoadmapCapacityCommandHandler(
    IRoadmapCapacityRepository capacityRepository,
    IRoadmapDemandRepository demandRepository,
    IRoadmapProjectRepository projectRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpsertRoadmapCapacityCommand, RoadmapCapacityDto>
{
    public async Task<RoadmapCapacityDto> Handle(
        UpsertRoadmapCapacityCommand request,
        CancellationToken cancellationToken)
    {
        _ = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("RoadmapProject", request.ProjectId);

        var capacity = await capacityRepository.GetByProjectQuarterAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken);

        if (capacity is null)
        {
            capacity = RoadmapCapacity.Create(
                request.ProjectId,
                request.QuarterYear,
                request.QuarterNumber,
                request.CapacityHours,
                request.Observation);

            await capacityRepository.AddAsync(capacity, cancellationToken);
        }
        else
        {
            capacity.Update(request.CapacityHours, request.Observation);
            capacityRepository.Update(capacity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var demands = await demandRepository.GetByProjectAsync(
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            cancellationToken);

        return GetRoadmapCapacityQueryHandler.MapCapacity(
            capacity,
            request.ProjectId,
            request.QuarterYear,
            request.QuarterNumber,
            demands);
    }
}