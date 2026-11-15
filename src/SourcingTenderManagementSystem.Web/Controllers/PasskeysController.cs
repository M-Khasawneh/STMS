using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Policy="SignedInAccount"),AutoValidateAntiforgeryToken,EnableRateLimiting("passkey"),ResponseCache(Duration=0,Location=ResponseCacheLocation.None,NoStore=true),RequestSizeLimit(32768)]
public sealed class PasskeysController(PasskeyManagement passkeys,PasskeySite site):Controller {
 public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next){
  if(!site.Matches(Request)){Response.StatusCode=503;context.Result=View("Problem","Passkey sign-in requires HTTPS at the configured STMS origin. Password sign-in remains available.");return;}
  var result=await next();if(result.Exception is BidException e){result.ExceptionHandled=true;if(e.Status==403)result.Result=Forbid();else if(e.Status==404)result.Result=NotFound();else{Response.StatusCode=e.Status;result.Result=View("Problem",e.Message);}}
  else if(result.Exception is InvalidOperationException or System.Text.Json.JsonException or ArgumentException or KeyNotFoundException){result.ExceptionHandled=true;Response.StatusCode=400;result.Result=View("Problem","The passkey request could not be completed. Start again or use your password.");}
 }
 [HttpGet]public async Task<IActionResult> Index()=>View(await passkeys.Page(User));
 [HttpPost]public async Task<IActionResult> EnrollOptions(PasskeyEnrollForm form){if(!ModelState.IsValid)return Invalid();return Content(await passkeys.BeginEnroll(User,form),"application/json");}
 [HttpPost]public async Task<IActionResult> Enroll(PasskeyCredentialForm form){if(!ModelState.IsValid)return Invalid();await passkeys.FinishEnroll(User,form.CredentialJson);TempData["PasskeyStatus"]="Passkey registered. Your device may use face, fingerprint or PIN.";return Json(new{redirectUrl=Url.Action(nameof(Index))});}
 [HttpPost]public async Task<IActionResult> Remove(PasskeyRemoveForm form){if(!ModelState.IsValid)return Invalid();await passkeys.Remove(User,form);TempData["PasskeyStatus"]="Passkey removed from STMS. You may also remove its saved copy in your device settings.";return RedirectToAction(nameof(Index));}
 [AllowAnonymous,HttpPost]public async Task<IActionResult> LoginOptions()=>Content(await passkeys.BeginLogin(),"application/json");
 [AllowAnonymous,HttpPost]public async Task<IActionResult> Login(PasskeyCredentialForm form){if(!ModelState.IsValid)return Invalid();await passkeys.FinishLogin(form.CredentialJson);return Json(new{redirectUrl=Url.Action("Index","Workspace")});}
 [AllowAnonymous,HttpPost]public async Task<IActionResult> Cancel(){await passkeys.Cancel();return NoContent();}
 private IActionResult Invalid(){Response.StatusCode=400;return View("Problem","Required passkey information is missing or invalid. Start again. Passwords and credentials are never redisplayed.");}
}
