using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

/// <summary>Adds the display name to the auth cookie / bearer token so pages don't hit the database for it.</summary>
public sealed class AppClaimsFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("full_name", string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "" : user.FullName));
        return identity;
    }
}
