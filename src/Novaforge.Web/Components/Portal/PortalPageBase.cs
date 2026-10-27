using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Novaforge.Web.Services;

namespace Novaforge.Web.Components.Portal;

/// <summary>Base for interactive portal pages: resolves the signed-in user once and turns service errors into messages.</summary>
public abstract class PortalPageBase : ComponentBase
{
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    protected CurrentUser? Me { get; private set; }
    protected string? ErrorMessage { get; set; }
    protected bool IsNotFound { get; set; }

    protected async Task<CurrentUser> GetUserAsync()
    {
        if (Me is not null)
        {
            return Me;
        }
        var state = AuthState is null ? null : await AuthState;
        Me = CurrentUser.From(state?.User) ?? throw new UnauthorizedAccessException();
        return Me;
    }

    /// <summary>Runs a service call; validation and access problems become a visible message instead of a crash.</summary>
    protected async Task<bool> TryAsync(Func<Task> action)
    {
        ErrorMessage = null;
        try
        {
            await action();
            return true;
        }
        catch (ValidationFailedException ex)
        {
            ErrorMessage = string.Join(" ", ex.Errors.SelectMany(e => e.Value));
        }
        catch (NotFoundException)
        {
            IsNotFound = true;
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "You don't have permission to do that.";
        }
        catch (PaymentGatewayException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (AiUnavailableException ex)
        {
            ErrorMessage = ex.Message;
        }
        return false;
    }
}
