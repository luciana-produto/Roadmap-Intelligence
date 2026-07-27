namespace ProductHub.Application.Access;

/// <summary>
/// Configuração de acesso. Os e-mails super-admin têm acesso total e são os únicos
/// que podem gerenciar as permissões dos demais usuários. Lidos da seção "Authorization".
/// </summary>
public sealed class AccessOptions
{
    public const string SectionName = "Authorization";

    /// <summary>Acesso total + gestão de acessos.</summary>
    public string[] SuperAdminEmails { get; set; } = [];

    /// <summary>Permissão de edição do roadmap concedida por configuração (ex.: dev logins).</summary>
    public string[] RoadmapEditEmails { get; set; } = [];

    /// <summary>Permissão de cadastros concedida por configuração (ex.: dev logins).</summary>
    public string[] RegistrationsEmails { get; set; } = [];
}
