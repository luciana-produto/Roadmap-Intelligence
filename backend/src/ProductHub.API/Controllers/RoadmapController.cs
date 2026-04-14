using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProductHub.Application.Roadmap.Commands.CreateDemand;
using ProductHub.Application.Roadmap.Commands.DeleteDemand;
using ProductHub.Application.Roadmap.Commands.ReorderDemand;
using ProductHub.Application.Roadmap.Commands.UpsertCapacity;
using ProductHub.Application.Roadmap.Commands.UpdateDemand;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Application.Roadmap.Queries.GetCapacity;
using ProductHub.Application.Roadmap.Queries.GetDependencyOptions;
using ProductHub.Application.Roadmap.Queries.GetRoadmap;
using ProductHub.Shared.Models;

namespace ProductHub.API.Controllers;

[Route("api/roadmap")]
public sealed class RoadmapController(ISender sender) : ApiControllerBase
{
    [HttpGet("demands")]
    public async Task<IActionResult> GetDemands(
        [FromQuery] Guid projectId,
        [FromQuery] int? quarterYear,
        [FromQuery] int? quarterNumber,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetDemandsQuery(projectId, quarterYear, quarterNumber),
            cancellationToken);
        return Ok(ApiResponse<IEnumerable<RoadmapDemandDto>>.Ok(result, CorrelationId));
    }

    [HttpGet("demands/dependency-options")]
    public async Task<IActionResult> GetDependencyOptions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDemandDependencyOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<IEnumerable<DemandDependencyOptionDto>>.Ok(result, CorrelationId));
    }

    [HttpGet("capacity")]
    public async Task<IActionResult> GetCapacity(
        [FromQuery] Guid projectId,
        [FromQuery] int quarterYear,
        [FromQuery] int quarterNumber,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetRoadmapCapacityQuery(projectId, quarterYear, quarterNumber),
            cancellationToken);
        return Ok(ApiResponse<RoadmapCapacityDto>.Ok(result, CorrelationId));
    }

    [HttpPut("capacity")]
    public async Task<IActionResult> UpsertCapacity(
        [FromBody] UpsertRoadmapCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Ok(ApiResponse<RoadmapCapacityDto>.Ok(result, CorrelationId));
    }

    [HttpPost("demands")]
    public async Task<IActionResult> CreateDemand(
        [FromBody] CreateRoadmapDemandCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(201, ApiResponse<RoadmapDemandDto>.Ok(result, CorrelationId));
    }

    [HttpPut("demands/{id:guid}")]
    public async Task<IActionResult> UpdateDemand(
        Guid id,
        [FromBody] UpdateRoadmapDemandCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);
        return Ok(ApiResponse<RoadmapDemandDto>.Ok(result, CorrelationId));
    }

    [HttpDelete("demands/{id:guid}")]
    public async Task<IActionResult> DeleteDemand(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRoadmapDemandCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }

    [HttpPut("demands/reorder")]
    public async Task<IActionResult> ReorderDemand(
        [FromBody] ReorderRoadmapDemandCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }

}
