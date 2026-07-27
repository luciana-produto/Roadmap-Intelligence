using MediatR;

namespace ProductHub.Application.Roadmap.Queries.GetDeletedDemands;

public sealed record GetDeletedDemandsQuery : IRequest<IReadOnlyList<DeletedDemandDto>>;

public sealed record DeletedDemandDto(
    Guid Id,
    string ItemType,
    string Title,
    int QuarterYear,
    int QuarterNumber,
    Guid? ProjectId,
    string? ProjectName,
    Guid? ParentDemandId,
    DateTime? DeletedAt,
    string? DeletedByEmail);
