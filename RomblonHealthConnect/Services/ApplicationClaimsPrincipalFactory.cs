using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RomblonHealthConnect.Models.Identity;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Adds the assigned facility and display details to the authentication cookie
/// at sign-in.
///
/// Putting HospitalId in a claim is what makes hospital-level scoping safe: the
/// value is signed into the cookie by the server, so a user cannot widen their
/// own scope by editing a form field, route value, or API parameter.
/// </summary>
public class ApplicationClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public ApplicationClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.HospitalId.HasValue)
        {
            identity.AddClaim(new Claim(
                CurrentUserService.HospitalIdClaim,
                user.HospitalId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(CurrentUserService.DisplayNameClaim, user.DisplayName));
        }

        if (!string.IsNullOrWhiteSpace(user.PositionTitle))
        {
            identity.AddClaim(new Claim(CurrentUserService.PositionTitleClaim, user.PositionTitle));
        }

        return identity;
    }
}
