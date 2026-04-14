using MediatR;
using Microsoft.AspNetCore.Mvc;
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
}
