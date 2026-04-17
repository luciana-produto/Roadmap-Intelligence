using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProductHub.Application.Roadmap.Commands.CreateDemandKpiMeasurement;
using ProductHub.Application.Roadmap.Commands.DeleteDemandKpiMeasurement;
using ProductHub.Application.Roadmap.Commands.CreateKpi;
using ProductHub.Application.Roadmap.Commands.DeleteKpi;
using ProductHub.Application.Roadmap.Commands.UpdateDemandKpiMeasurement;
using ProductHub.Application.Roadmap.Commands.UpdateDemandKpiLinks;
using ProductHub.Application.Roadmap.Commands.UpdateKpi;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Application.Roadmap.Queries.GetDemandKpiMeasurements;
using ProductHub.Application.Roadmap.Queries.GetKpis;
using ProductHub.Shared.Models;

namespace ProductHub.API.Controllers;

[Route("api/kpis")]
public sealed class KpiController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(
        [FromQuery] Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetKpisByProjectQuery(projectId), cancellationToken);
        return Ok(ApiResponse<IEnumerable<KpiDto>>.Ok(result, CorrelationId));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateKpiCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(201, ApiResponse<KpiDto>.Ok(result, CorrelationId));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateKpiCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return Ok(ApiResponse<KpiDto>.Ok(result, CorrelationId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKpiCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }

    [HttpPut("demands/{demandId:guid}/links")]
    public async Task<IActionResult> UpdateDemandKpiLinks(
        Guid demandId,
        [FromBody] UpdateDemandKpiLinksCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { DemandId = demandId }, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DemandKpiLinkDto>>.Ok(result, CorrelationId));
    }

    [HttpGet("demands/{demandId:guid}/measurements")]
    public async Task<IActionResult> GetDemandKpiMeasurements(
        Guid demandId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDemandKpiMeasurementsQuery(demandId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<KpiMeasurementDto>>.Ok(result, CorrelationId));
    }

    [HttpPost("demands/{demandId:guid}/measurements")]
    public async Task<IActionResult> CreateDemandKpiMeasurement(
        Guid demandId,
        [FromBody] CreateDemandKpiMeasurementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { DemandId = demandId }, cancellationToken);
        return StatusCode(201, ApiResponse<KpiMeasurementDto>.Ok(result, CorrelationId));
    }

    [HttpPut("measurements/{id:guid}")]
    public async Task<IActionResult> UpdateDemandKpiMeasurement(
        Guid id,
        [FromBody] UpdateDemandKpiMeasurementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return Ok(ApiResponse<KpiMeasurementDto>.Ok(result, CorrelationId));
    }

    [HttpDelete("measurements/{id:guid}")]
    public async Task<IActionResult> DeleteDemandKpiMeasurement(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDemandKpiMeasurementCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }
}
