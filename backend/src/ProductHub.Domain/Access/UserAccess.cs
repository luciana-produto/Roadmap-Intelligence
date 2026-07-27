using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;

namespace ProductHub.Domain.Access;

/// <summary>
/// Permissões de acesso de um usuário, identificado pelo e-mail do SSO (normalizado).
/// Ausência de registro = usuário apenas visualiza (sem permissões adicionais).
/// </summary>
public sealed class UserAccess : BaseEntity, IAuditableEntity
{
    public string Email { get; private set; } = default!;
    public bool CanEditRoadmap { get; private set; }
    public bool CanManageRegistrations { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private UserAccess() { }

    public static UserAccess Create(string email, bool canEditRoadmap, bool canManageRegistrations) =>
        new()
        {
            Email = NormalizeEmail(email),
            CanEditRoadmap = canEditRoadmap,
            CanManageRegistrations = canManageRegistrations
        };

    public void SetPermissions(bool canEditRoadmap, bool canManageRegistrations)
    {
        CanEditRoadmap = canEditRoadmap;
        CanManageRegistrations = canManageRegistrations;
    }

    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}
