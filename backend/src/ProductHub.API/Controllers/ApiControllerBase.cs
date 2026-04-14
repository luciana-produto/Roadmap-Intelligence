using Microsoft.AspNetCore.Mvc;
using ProductHub.Shared.Constants;

namespace ProductHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string CorrelationId =>
        HttpContext.Items.TryGetValue(AppConstants.Http.CorrelationIdHeader, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
}
