using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetKpis;

public sealed record GetKpisByProjectQuery(Guid ProjectId) : IRequest<IEnumerable<KpiDto>>;
