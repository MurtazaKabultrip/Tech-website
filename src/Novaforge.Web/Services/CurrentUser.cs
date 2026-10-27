using System.Security.Claims;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

/// <summary>The caller of a service operation. Every data access is checked against this.</summary>
public sealed record CurrentUser(string Id, string Name, bool IsAdmin)
{
    public static CurrentUser? From(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var name = principal.FindFirstValue("full_name")
                   ?? principal.FindFirstValue(ClaimTypes.Name)
                   ?? "User";
        return new CurrentUser(id, name, principal.IsInRole(Roles.Admin));
    }
}

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Validation failed")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationFailedException Single(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
