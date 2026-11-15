using System.Security.Cryptography;
namespace SourcingTenderManagementSystem.Web.Security;
public sealed record PasskeyTicket(string Operation,string Challenge,string? UserId,string? Stamp,string? Name,DateTimeOffset Expires);
// Atomic one-use server state supplements Identity's protected ceremony cookie, preventing cookie replay.
// Single application instance; restart invalidates pending ceremonies safely.
public sealed class PasskeyCeremonies(TimeProvider clock) {
 private readonly Dictionary<string,PasskeyTicket> entries=[];private readonly object sync=new();
 public SemaphoreSlim Writes {get;}=new(1,1);
 public string Issue(PasskeyTicket ticket){lock(sync){foreach(var key in entries.Where(e=>e.Value.Expires<=clock.GetUtcNow()).Select(e=>e.Key).ToArray())entries.Remove(key);if(entries.Count>=2000)throw new InvalidOperationException("Passkey capacity reached");var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));entries.Add(token,ticket);return token;}}
 public PasskeyTicket? Take(string? token){if(token is null)return null;lock(sync){return entries.Remove(token,out var ticket)&&ticket.Expires>clock.GetUtcNow()?ticket:null;}}
}
