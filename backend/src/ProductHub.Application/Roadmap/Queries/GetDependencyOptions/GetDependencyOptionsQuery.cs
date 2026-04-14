using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Queries.GetDependencyOptions;

public sealed record GetDemandDependencyOptionsQuery() : IRequest<IEnumerable<DemandDependencyOptionDto>>;