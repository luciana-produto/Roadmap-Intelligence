using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProductHub.Application.Roadmap.Commands.CreateProject;
using ProductHub.Application.Roadmap.Commands.CreateProjectProduct;
using ProductHub.Application.Roadmap.Commands.DeleteProject;
using ProductHub.Application.Roadmap.Commands.DeleteProjectProduct;
using ProductHub.Application.Roadmap.Commands.UpdateProject;
using ProductHub.Application.Roadmap.Commands.UpdateProjectProduct;
using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Application.Roadmap.Queries.GetProjects;
using ProductHub.Shared.Models;

namespace ProductHub.API.Controllers;

[Route("api/projects")]
public sealed class ProjectsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProjectsQuery(), cancellationToken);
        return Ok(ApiResponse<IEnumerable<RoadmapProjectDto>>.Ok(result, CorrelationId));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoadmapProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await sender.Send(command, cancellationToken);
        return StatusCode(201, ApiResponse<RoadmapProjectDto>.Ok(project, CorrelationId));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRoadmapProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await sender.Send(command with { Id = id }, cancellationToken);
        return Ok(ApiResponse<RoadmapProjectDto>.Ok(project, CorrelationId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRoadmapProjectCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }

    [HttpPost("{projectId:guid}/products")]
    public async Task<IActionResult> CreateProduct(
        Guid projectId,
        [FromBody] CreateRoadmapProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await sender.Send(command with { ProjectId = projectId }, cancellationToken);
        return StatusCode(201, ApiResponse<RoadmapProductDto>.Ok(product, CorrelationId));
    }

    [HttpPut("{projectId:guid}/products/{productId:guid}")]
    public async Task<IActionResult> UpdateProduct(
        Guid projectId,
        Guid productId,
        [FromBody] UpdateRoadmapProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await sender.Send(
            command with { ProjectId = projectId, ProductId = productId },
            cancellationToken);

        return Ok(ApiResponse<RoadmapProductDto>.Ok(product, CorrelationId));
    }

    [HttpDelete("{projectId:guid}/products/{productId:guid}")]
    public async Task<IActionResult> DeleteProduct(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRoadmapProductCommand(projectId, productId), cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }
}
