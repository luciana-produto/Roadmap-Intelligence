using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteKpi;

public sealed class DeleteKpiCommandHandler(
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteKpiCommand>
{
    public async Task Handle(
        DeleteKpiCommand request,
        CancellationToken cancellationToken)
    {
        var kpi = await kpiRepository.GetByIdTrackedAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kpi", request.Id);

        kpiRepository.Remove(kpi);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
