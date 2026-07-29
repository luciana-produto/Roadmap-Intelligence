using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.UpdateKpi;

public sealed record UpdateKpiCommand(
    Guid Id,
    string Name,
    string Type,
    string Category,
    string Indicator,
    string Operation,
    IReadOnlyList<string> AllowedUnits,
    string? Description = null) : IRequest<KpiDto>;
