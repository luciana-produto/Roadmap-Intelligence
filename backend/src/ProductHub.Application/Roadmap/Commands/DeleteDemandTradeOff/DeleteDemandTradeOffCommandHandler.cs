using MediatR;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Commands.DeleteDemandTradeOff;

public sealed class DeleteDemandTradeOffCommandHandler(
    IKpiRepository kpiRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteDemandTradeOffCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDemandTradeOffCommand request, CancellationToken cancellationToken)
    {
        var tradeOff = await kpiRepository.GetTradeOffByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("DemandTradeOff", request.Id);

        await kpiRepository.RemoveTradeOff(tradeOff);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}