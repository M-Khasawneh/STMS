using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = "SignedInAccount"), AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ProfileController(ProfileManagement profiles, SignInManager<IdentityUser> signIn) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        if (result.Exception is UnauthorizedAccessException) { result.ExceptionHandled = true; result.Result = Forbid(); }
    }
    [HttpGet] public async Task<IActionResult> Index() => View(await profiles.ReadAsync(User));
    [HttpPost] public async Task<IActionResult> Contact([Bind(Prefix = "Contact")] ProfileContactForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            var result = await profiles.ContactAsync(User, form, ct);
            if (result.Succeeded) return await Saved("Profile information updated.");
            AddErrors(result);
        }
        var page = await profiles.ReadAsync(User); page.Contact = form; return View("Index", page);
    }
    [HttpPost] public async Task<IActionResult> Password([Bind(Prefix = "Password")] ProfilePasswordForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            var result = await profiles.PasswordAsync(User, form, ct);
            if (result.Succeeded) return await Saved("Password changed. Your other sessions will need to sign in again.");
            AddErrors(result);
        }
        // Preserve validation errors, never submitted password values.
        foreach (var key in new[] { "Password.CurrentPassword", "Password.NewPassword", "Password.ConfirmPassword" }) ModelState.SetModelValue(key, "", "");
        return View("Index", await profiles.ReadAsync(User));
    }
    private void AddErrors(IdentityResult result) { foreach (var error in result.Errors) ModelState.AddModelError("", error.Description); }
    private async Task<IActionResult> Saved(string message)
    {
        await signIn.RefreshSignInAsync(await profiles.UserAsync(User));
        TempData["ProfileStatus"] = message; return RedirectToAction(nameof(Index));
    }
}
