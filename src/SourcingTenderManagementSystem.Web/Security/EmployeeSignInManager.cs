using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SourcingTenderManagementSystem.Web.Security;

public sealed class EmployeeSignInManager(
    UserManager<IdentityUser> users, IHttpContextAccessor accessor,
    IUserClaimsPrincipalFactory<IdentityUser> claimsFactory, IOptions<IdentityOptions> options,
    ILogger<SignInManager<IdentityUser>> logger, IAuthenticationSchemeProvider schemes,
    IUserConfirmation<IdentityUser> confirmation, IEmployeeEligibility eligibility)
    : SignInManager<IdentityUser>(users, accessor, claimsFactory, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(IdentityUser user) =>
        await base.CanSignInAsync(user) && await eligibility.IsEligibleAsync(user);

    public async Task<SignInResult> CameraSignInAsync(IdentityUser user)
    {
        var rejected = await PreSignInCheck(user);
        if (rejected is not null) return rejected;
        if (await IsTwoFactorEnabledAsync(user) && !await IsTwoFactorClientRememberedAsync(user))
            return await SignInOrTwoFactorAsync(user, isPersistent: false, loginProvider: "Camera");
        if (!(await UserManager.ResetAccessFailedCountAsync(user)).Succeeded) return SignInResult.Failed;
        // Preserve Identity's two-factor requirements instead of issuing a cookie directly.
        return await SignInOrTwoFactorAsync(user, isPersistent: false, loginProvider: "Camera");
    }
}
