using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;
namespace SourcingTenderManagementSystem.Web.Security;
public sealed class PasskeyManagement(UserManager<IdentityUser> users,SignInManager<IdentityUser> signIn,IEmployeeEligibility eligibility,PasskeyCeremonies ceremonies,TimeProvider clock) {
 private const string Cookie="__Host-STMS.Passkey";
 private HttpContext Context=>signIn.Context;
 private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition,int status,string message){if(!condition)throw new BidException(status,message);}
 private async Task<IdentityUser> Actor(ClaimsPrincipal principal){var user=await users.GetUserAsync(principal);Require(user is not null&&!await users.IsLockedOutAsync(user)&&await signIn.CanSignInAsync(user)&&await eligibility.IsEligibleAsync(user),403,"This account is not currently eligible.");return user!;}
 private async Task Password(IdentityUser user,string password){Require(!string.IsNullOrEmpty(password)&&password.Length<=256,400,"Enter your current password.");var result=await signIn.CheckPasswordSignInAsync(user,password,lockoutOnFailure:true);Require(result.Succeeded,400,"Unable to verify the account. Check your password or contact your administrator.");}
 private void Issue(string operation,string options,IdentityUser? user=null,string? name=null){ceremonies.Take(Context.Request.Cookies[Cookie]);using var parsed=System.Text.Json.JsonDocument.Parse(options);var challenge=parsed.RootElement.GetProperty("challenge").GetString()!;var token=ceremonies.Issue(new(operation,challenge,user?.Id,user?.SecurityStamp,name,clock.GetUtcNow().AddMinutes(3)));Context.Response.Cookies.Append(Cookie,token,new(){HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,Path="/",MaxAge=TimeSpan.FromMinutes(3),IsEssential=true});}
 private PasskeyTicket Consume(string operation,string json){var ticket=ceremonies.Take(Context.Request.Cookies[Cookie]);Context.Response.Cookies.Delete(Cookie,new(){HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,Path="/"});Require(ticket?.Operation==operation,400,"This passkey request expired or was already used. Start again.");using var credential=System.Text.Json.JsonDocument.Parse(json);var encoded=credential.RootElement.GetProperty("response").GetProperty("clientDataJSON").GetString()!;using var client=System.Text.Json.JsonDocument.Parse(WebEncoders.Base64UrlDecode(encoded));
  Require(client.RootElement.GetProperty("challenge").GetString()==ticket.Challenge,400,"The passkey challenge changed. Start again.");return ticket!;}
 public async Task<PasskeyPage> Page(ClaimsPrincipal principal){var user=await Actor(principal);return new((await users.GetPasskeysAsync(user)).Select(p=>new PasskeyRow(WebEncoders.Base64UrlEncode(p.CredentialId),p.Name??"Passkey",p.CreatedAt)).ToArray());}
 public async Task<string> BeginEnroll(ClaimsPrincipal principal,PasskeyEnrollForm form){
  var user=await Actor(principal);await Password(user,form.CurrentPassword);var name=form.Name?.Trim();Require(!string.IsNullOrWhiteSpace(name)&&name.Length<=80,400,"Enter a passkey name of at most 80 characters.");
  Require((await users.GetPasskeysAsync(user)).Count<5,400,"At most five passkeys can be registered. Remove an old key first.");
  var json=await signIn.MakePasskeyCreationOptionsAsync(new(){Id=user.Id,Name=user.UserName??user.Id,DisplayName=user.UserName??user.Id});Issue("enroll",json,user,name);return json;
 }
 public async Task FinishEnroll(ClaimsPrincipal principal,string json){
  var ticket=Consume("enroll",json);await ceremonies.Writes.WaitAsync(Context.RequestAborted);
  try{var user=await Actor(principal);Require(ticket.UserId==user.Id&&ticket.Stamp==user.SecurityStamp,403,"Account or security settings changed. Start again.");
   var result=await signIn.PerformPasskeyAttestationAsync(json);
   Require(result.Succeeded&&result.UserEntity.Id==user.Id&&result.Passkey.IsUserVerified,400,"The passkey could not be verified. Start again.");
   Require((await users.GetPasskeysAsync(user)).Count<5,400,"At most five passkeys are allowed.");
   Require(await users.FindByPasskeyIdAsync(result.Passkey.CredentialId)is null,409,"This credential is already registered.");
   await Actor(principal);result.Passkey.Name=ticket.Name;Require((await users.AddOrUpdatePasskeyAsync(user,result.Passkey)).Succeeded,409,"The passkey could not be saved. Reload and try again.");
  }finally{ceremonies.Writes.Release();await Context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);}
 }
 public async Task<string> BeginLogin(){Require(Context.User.Identity?.IsAuthenticated!=true,409,"You are already signed in.");var json=await signIn.MakePasskeyRequestOptionsAsync(null);Issue("login",json);return json;}
 public async Task FinishLogin(string json){Consume("login",json);Require(Context.User.Identity?.IsAuthenticated!=true,409,"You are already signed in.");await ceremonies.Writes.WaitAsync(Context.RequestAborted);
  try{var result=await signIn.PasskeySignInAsync(json);Require(result.Succeeded,400,"Unable to sign in with this passkey. Try again, use your password, or contact your administrator.");}
  finally{ceremonies.Writes.Release();await Context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);}
 }
 public async Task Remove(ClaimsPrincipal principal,PasskeyRemoveForm form){await ceremonies.Writes.WaitAsync(Context.RequestAborted);try{
  var user=await Actor(principal);await Password(user,form.CurrentPassword);byte[] id;try{id=WebEncoders.Base64UrlDecode(form.CredentialId);}catch(FormatException){throw new BidException(400,"Invalid passkey identifier.");}
  Require(id.Length is >0 and <=1024,400,"Invalid passkey identifier.");var existing=(await users.GetPasskeysAsync(user)).SingleOrDefault(p=>p.CredentialId.AsSpan().SequenceEqual(id));Require(existing is not null,404,"Passkey not found for this account.");
  Require((await users.RemovePasskeyAsync(user,id)).Succeeded,409,"The passkey could not be removed.");
 }finally{ceremonies.Writes.Release();}}
 public async Task Cancel(){ceremonies.Take(Context.Request.Cookies[Cookie]);Context.Response.Cookies.Delete(Cookie,new(){Secure=true,HttpOnly=true,SameSite=SameSiteMode.Strict,Path="/"});await Context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);}
}
