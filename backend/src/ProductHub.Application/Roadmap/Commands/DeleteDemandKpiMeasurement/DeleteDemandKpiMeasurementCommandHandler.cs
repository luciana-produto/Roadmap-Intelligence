using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemandKpiMeasurement;

public sealed class DeleteDemandKpiMeasurementCommandHandler(
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteDemandKpiMeasurementCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDemandKpiMeasurementCommand request, CancellationToken cancellationToken)
    {
        var measurement = await kpiRepository.GetMeasurementByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KpiMeasurement", request.Id);

        await kpiRepository.RemoveMeasurement(measurement);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}