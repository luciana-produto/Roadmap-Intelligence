namespace ProductHub.Domain.Errors;

public sealed record DomainError(string Code, string Description)
{
    public static readonly DomainError None = new(string.Empty, string.Empty);

    public static DomainError NotFound(string entity, object id) =>
        new($"{entity}.NotFound", $"'{entity}' with id '{id}' was not found.");

    public static DomainError Validation(string field, string message) =>
        new($"{field}.Validation", message);

    public static DomainError Conflict(string entity) =>
        new($"{entity}.Conflict", $"A conflict occurred for '{entity}'.");
}
