namespace ProductHub.Application.Common;

/// <summary>Acesso ao usuário autenticado da requisição atual.</summary>
public interface ICurrentUserService
{
    /// <summary>E-mail do usuário logado (claim do SSO), ou null quando não autenticado.</summary>
    string? Email { get; }
}
