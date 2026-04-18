using MediatR;
using ProductHub.Application.Roadmap.DTOs;

namespace ProductHub.Application.Roadmap.Commands.CreateKpi;

public sealed record CreateKpiCommand(
    string Name,
    string Type,
    string Lever,
    string Objective,
    string? Description = null,
    string? Calculation = null,
    decimal? Target = null,
    decimal? CurrentValue = null) : IRequest<KpiDto>;
