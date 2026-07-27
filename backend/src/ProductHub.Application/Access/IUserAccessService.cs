namespace ProductHub.Application.Access;

/// <summary>Permissões efetivas do usuário atual.</summary>
public sealed record EffectiveAccess(
    bool CanEditRoadmap,
    bool CanManageRegistrations,
    bool CanManageAccess);

/// <summary>Um registro de permissão (uma pessoa).</summary>
public sealed record UserAccessEntry(
    string Email,
    bool CanEditRoadmap,
    bool CanManageRegistrations,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Lista de permissões: super-admins (config) + usuários cadastrados no banco.</summary>
public sealed record UserAccessList(
    IReadOnlyList<string> SuperAdminEmails,
    IReadOnlyList<UserAccessEntry> Users);

public interface IUserAccessService
{
    Task<EffectiveAccess> GetEffectiveAsync(string? email, CancellationToken cancellationToken = default);
    Task<UserAccessList> ListAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(string email, bool canEditRoadmap, bool canManageRegistrations, CancellationToken cancellationToken = default);
    Task DeleteAsync(string email, CancellationToken cancellationToken = default);
}
