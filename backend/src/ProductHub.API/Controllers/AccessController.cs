using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductHub.API.Security;
using ProductHub.Application.Access;
using ProductHub.Shared.Models;

namespace ProductHub.API.Controllers;

[Route("api/access")]
public sealed class AccessController(IUserAccessService accessService) : ApiControllerBase
{
    /// <summary>Permissões efetivas do usuário logado. Disponível a qualquer autenticado.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var access = await accessService.GetEffectiveAsync(email, cancellationToken);
        return Ok(ApiResponse<EffectiveAccess>.Ok(access, CorrelationId));
    }

    /// <summary>Lista super-admins (config) + usuários com permissões cadastradas.</summary>
    [HttpGet]
    [Authorize(Policy = AccessPolicies.ManageAccess)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await accessService.ListAsync(cancellationToken);
        return Ok(ApiResponse<UserAccessList>.Ok(result, CorrelationId));
    }

    /// <summary>Cria/atualiza as permissões de um e-mail. Sem permissões = remove o registro.</summary>
    [HttpPut]
    [Authorize(Policy = AccessPolicies.ManageAccess)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertAccessRequest request,
        CancellationToken cancellationToken)
    {
        await accessService.UpsertAsync(request.Email, request.CanEditRoadmap, request.CanManageRegistrations, cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }

    /// <summary>Remove as permissões de um e-mail.</summary>
    [HttpDelete]
    [Authorize(Policy = AccessPolicies.ManageAccess)]
    public async Task<IActionResult> Delete(
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        await accessService.DeleteAsync(email, cancellationToken);
        return Ok(ApiResponse.Ok(CorrelationId));
    }

    public sealed record UpsertAccessRequest(string Email, bool CanEditRoadmap, bool CanManageRegistrations);
}
