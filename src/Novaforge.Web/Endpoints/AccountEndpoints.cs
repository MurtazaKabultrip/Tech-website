using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // Form post with antiforgery token (validated by UseAntiforgery).
        app.MapPost("/account/logout", async (SignInManager<AppUser> signInManager, [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
                ? returnUrl
                : "/";
            return TypedResults.LocalRedirect(target);
        });
        return app;
    }
}
