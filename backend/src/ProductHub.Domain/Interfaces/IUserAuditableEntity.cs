namespace ProductHub.Domain.Interfaces;

/// <summary>
/// Entidade que registra QUEM criou e QUEM alterou por último (e-mail do SSO),
/// além das datas de <see cref="IAuditableEntity"/>. Preenchido pelo interceptor.
/// </summary>
public interface IUserAuditableEntity : IAuditableEntity
{
    string? CreatedByEmail { get; set; }
    string? UpdatedByEmail { get; set; }
}
