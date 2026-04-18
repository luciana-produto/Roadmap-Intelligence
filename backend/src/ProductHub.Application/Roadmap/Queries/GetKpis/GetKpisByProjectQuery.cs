using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetKpis;

public sealed record GetKpisQuery() : IRequest<IEnumerable<KpiDto>>;
