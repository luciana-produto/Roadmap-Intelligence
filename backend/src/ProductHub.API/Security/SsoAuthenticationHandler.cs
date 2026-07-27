using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ProductHub.API.Security;

/// <summary>
/// Opções do esquema de autenticação por sessão SSO.
/// </summary>
public sealed class SsoAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>URL base do SSO (ex.: https://sso-auth-hub.linxfood.com.br).</summary>
    public string SsoBaseUrl { get; set; } = string.Empty;

    /// <summary>Tenant configurado no SSO (ex.: linx).</summary>
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>
    /// Quando true, aceita a sessão de desenvolvimento local sem chamar o SSO.
    /// Deve ser habilitado APENAS em ambiente de desenvolvimento.
    /// </summary>
    public bool AllowDevSession { get; set; }
}

/// <summary>
/// Valida o usuário a partir do sessionId enviado pelo frontend (header Authorization: Bearer).
/// A sessão é opaca: a validação ocorre consultando o endpoint de usuário do SSO.
/// O resultado é cacheado por alguns minutos para não sobrecarregar o SSO.
/// </summary>
public sealed class SsoAuthenticationHandler : AuthenticationHandler<SsoAuthenticationOptions>
{
    public const string SchemeName = "Sso";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Sessões de desenvolvimento local (só quando AllowDevSession). Cada token mapeia para um
    // e-mail distinto; as permissões desses e-mails vêm da configuração (Authorization:*Emails).
    private static readonly Dictionary<string, (string Email, string Name)> DevSessions = new(StringComparer.Ordinal)
    {
        ["dev-session"] = ("dev@producthub.local", "Dev User"),
        ["dev-session-roadmap"] = ("dev-roadmap@producthub.local", "Dev Roadmap"),
        ["dev-session-cadastros"] = ("dev-cadastros@producthub.local", "Dev Cadastros"),
        ["dev-session-full"] = ("dev-full@producthub.local", "Dev Full")
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public SsoAuthenticationHandler(
        IOptionsMonitor<SsoAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
        : base(options, logger, encoder)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sessionId = ExtractSessionId();
        if (string.IsNullOrWhiteSpace(sessionId))
            return AuthenticateResult.NoResult();

        // Bypass de desenvolvimento local: mantém os "Dev Login (local only)" funcionando.
        if (Options.AllowDevSession && DevSessions.TryGetValue(sessionId, out var dev))
            return Success(BuildDevPrincipal(dev.Email, dev.Name));

        if (_cache.TryGetValue(CacheKey(sessionId), out ClaimsPrincipal? cached) && cached is not null)
            return Success(cached);

        var principal = await ValidateWithSsoAsync(sessionId);
        if (principal is null)
            return AuthenticateResult.Fail("Sessão inválida ou expirada.");

        _cache.Set(CacheKey(sessionId), principal, CacheDuration);
        return Success(principal);
    }

    private string? ExtractSessionId()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
            return null;

        const string bearerPrefix = "Bearer ";
        return authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authHeader[bearerPrefix.Length..].Trim()
            : authHeader.Trim();
    }

    private async Task<ClaimsPrincipal?> ValidateWithSsoAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(Options.SsoBaseUrl) || string.IsNullOrWhiteSpace(Options.TenantKey))
        {
            Logger.LogError("SSO não configurado (Sso:BaseUrl / Sso:TenantKey). Não é possível autenticar.");
            return null;
        }

        var url = $"{Options.SsoBaseUrl.TrimEnd('/')}/api/auth/{Options.TenantKey}/user/{Uri.EscapeDataString(sessionId)}";

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("SSO recusou a sessão. Status: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
            var user = await JsonSerializer.DeserializeAsync<SsoUserResponse>(stream, cancellationToken: Context.RequestAborted);

            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                Logger.LogWarning("SSO retornou um usuário sem e-mail; sessão tratada como inválida.");
                return null;
            }

            return BuildPrincipal(user);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Falha ao validar a sessão no SSO.");
            return null;
        }
    }

    private static ClaimsPrincipal BuildPrincipal(SsoUserResponse user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id ?? user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name ?? user.Email)
        };

        foreach (var group in user.Groups ?? [])
        {
            if (!string.IsNullOrWhiteSpace(group))
                claims.Add(new Claim(ClaimTypes.Role, group));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
    }

    private static ClaimsPrincipal BuildDevPrincipal(string email, string name)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
    }

    private AuthenticateResult Success(ClaimsPrincipal principal) =>
        AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));

    private static string CacheKey(string sessionId) => $"sso-session:{sessionId}";

    private sealed record SsoUserResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("groups")]
        public string[]? Groups { get; init; }
    }
}
