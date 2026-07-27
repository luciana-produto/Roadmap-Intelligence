using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ProductHub.Application.Access;

namespace ProductHub.API.Security;

/// <summary>Nomes das policies e chaves de permissão usadas no enforcement.</summary>
public static class AccessPolicies
{
    public const string RoadmapEdit = "RoadmapEdit";
    public const string Registrations = "Registrations";
    public const string ManageAccess = "ManageAccess";

    public const string PermissionRoadmapEdit = "roadmap-edit";
    public const string PermissionRegistrations = "registrations";
    public const string PermissionManageAccess = "manage-access";
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Resolve a permissão do usuário atual (e-mail do claim) contra a config de super-admins
/// e a tabela de permissões. Executa por requisição, então mudanças refletem na hora.
/// </summary>
public sealed class PermissionAuthorizationHandler(IUserAccessService accessService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return;

        var access = await accessService.GetEffectiveAsync(email);

        var granted = requirement.Permission switch
        {
            AccessPolicies.PermissionRoadmapEdit => access.CanEditRoadmap,
            AccessPolicies.PermissionRegistrations => access.CanManageRegistrations,
            AccessPolicies.PermissionManageAccess => access.CanManageAccess,
            _ => false
        };

        if (granted)
            context.Succeed(requirement);
    }
}
