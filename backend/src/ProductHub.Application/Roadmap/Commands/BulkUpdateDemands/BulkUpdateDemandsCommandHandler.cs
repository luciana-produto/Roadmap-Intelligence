using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.BulkUpdateDemands;

public sealed class BulkUpdateDemandsCommandHandler(ISender sender)
    : IRequestHandler<BulkUpdateDemandsCommand, IReadOnlyList<RoadmapDemandDto>>
{
    public async Task<IReadOnlyList<RoadmapDemandDto>> Handle(
        BulkUpdateDemandsCommand request,
        CancellationToken cancellationToken)
    {
        var results = new List<RoadmapDemandDto>();

        // Reuse the existing per-item update logic (validation, products/links/dependencies, etc.).
        // Sequential because the DbContext isn't safe for concurrent use.
        foreach (var item in request.Items ?? [])
            results.Add(await sender.Send(item, cancellationToken));

        return results;
    }
}
