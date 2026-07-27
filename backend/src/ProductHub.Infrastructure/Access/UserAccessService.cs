using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProductHub.Application.Access;
using ProductHub.Domain.Access;
using ProductHub.Infrastructure.Persistence;

namespace ProductHub.Infrastructure.Access;

public sealed class UserAccessService(AppDbContext db, IOptions<AccessOptions> options) : IUserAccessService
{
    private readonly AppDbContext _db = db;

    private static HashSet<string> Normalize(string[]? emails) =>
        (emails ?? [])
            .Select(UserAccess.NormalizeEmail)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToHashSet();

    private readonly HashSet<string> _superAdmins = Normalize(options.Value.SuperAdminEmails);
    private readonly HashSet<string> _roadmapEditEmails = Normalize(options.Value.RoadmapEditEmails);
    private readonly HashSet<string> _registrationsEmails = Normalize(options.Value.RegistrationsEmails);

    public async Task<EffectiveAccess> GetEffectiveAsync(string? email, CancellationToken cancellationToken = default)
    {
        var normalized = UserAccess.NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return new EffectiveAccess(false, false, false);

        if (_superAdmins.Contains(normalized))
            return new EffectiveAccess(true, true, true);

        var entry = await _db.UserAccesses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);

        // Permissões vêm do banco (tela de Acessos) OU concedidas por configuração (dev logins).
        var canEditRoadmap = (entry?.CanEditRoadmap ?? false) || _roadmapEditEmails.Contains(normalized);
        var canManageRegistrations = (entry?.CanManageRegistrations ?? false) || _registrationsEmails.Contains(normalized);

        return new EffectiveAccess(canEditRoadmap, canManageRegistrations, false);
    }

    public async Task<UserAccessList> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await _db.UserAccesses.AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new UserAccessEntry(x.Email, x.CanEditRoadmap, x.CanManageRegistrations, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var superAdmins = _superAdmins.OrderBy(e => e).ToList();
        return new UserAccessList(superAdmins, users);
    }

    public async Task UpsertAsync(string email, bool canEditRoadmap, bool canManageRegistrations, CancellationToken cancellationToken = default)
    {
        var normalized = UserAccess.NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var entry = await _db.UserAccesses.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);

        // Nenhuma permissão marcada = equivalente a não ter registro: remove.
        if (!canEditRoadmap && !canManageRegistrations)
        {
            if (entry is not null)
            {
                _db.UserAccesses.Remove(entry);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        if (entry is null)
        {
            _db.UserAccesses.Add(UserAccess.Create(normalized, canEditRoadmap, canManageRegistrations));
        }
        else
        {
            entry.SetPermissions(canEditRoadmap, canManageRegistrations);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = UserAccess.NormalizeEmail(email);
        var entry = await _db.UserAccesses.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
        if (entry is not null)
        {
            _db.UserAccesses.Remove(entry);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
