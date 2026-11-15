using Microsoft.AspNetCore.Identity;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Test-only stores: no SQL connection, persistent users, or default credentials.
public sealed class MemoryUsers : IUserPasswordStore<IdentityUser>, IUserRoleStore<IdentityUser>,
    IUserSecurityStampStore<IdentityUser>, IUserLockoutStore<IdentityUser>,
    IUserEmailStore<IdentityUser>, IUserPhoneNumberStore<IdentityUser>, IUserPasskeyStore<IdentityUser>
{
    public Dictionary<string, IdentityUser> Users { get; } = [];
    public Dictionary<string, HashSet<string>> Roles { get; } = [];
    public Dictionary<string,Dictionary<string,UserPasskeyInfo>> Passkeys {get;}=[];
    public Task AddOrUpdatePasskeyAsync(IdentityUser user,UserPasskeyInfo passkey,CancellationToken ct){if(!Passkeys.TryGetValue(user.Id,out var keys))Passkeys[user.Id]=keys=[];keys[Convert.ToHexString(passkey.CredentialId)]=passkey;return Task.CompletedTask;}
    public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(IdentityUser user,CancellationToken ct)=>Task.FromResult<IList<UserPasskeyInfo>>(Passkeys.GetValueOrDefault(user.Id)?.Values.ToArray()??[]);
    public Task<IdentityUser?> FindByPasskeyIdAsync(byte[] id,CancellationToken ct)=>Task.FromResult(Users.Values.SingleOrDefault(u=>Passkeys.GetValueOrDefault(u.Id)?.ContainsKey(Convert.ToHexString(id))==true));
    public Task<UserPasskeyInfo?> FindPasskeyAsync(IdentityUser user,byte[] id,CancellationToken ct)=>Task.FromResult(Passkeys.GetValueOrDefault(user.Id)?.GetValueOrDefault(Convert.ToHexString(id)));
    public Task RemovePasskeyAsync(IdentityUser user,byte[] id,CancellationToken ct){Passkeys.GetValueOrDefault(user.Id)?.Remove(Convert.ToHexString(id));return Task.CompletedTask;}
    public bool FailNextUpdate { get; set; }
    public void Dispose() { }
    public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken token) { Users[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken token)
    {
        if (!FailNextUpdate) { Users[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
        FailNextUpdate = false;
        return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure", Description = "Simulated concurrent update failure." }));
    }
    public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken token) { Users.Remove(user.Id); return Task.FromResult(IdentityResult.Success); }
    public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(IdentityUser user, string? name, CancellationToken token) { user.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(IdentityUser user, string? name, CancellationToken token) { user.NormalizedUserName = name; return Task.CompletedTask; }
    public Task<IdentityUser?> FindByIdAsync(string id, CancellationToken token) => Task.FromResult(Users.GetValueOrDefault(id));
    public Task<IdentityUser?> FindByNameAsync(string name, CancellationToken token) => Task.FromResult(Users.Values.SingleOrDefault(user => user.NormalizedUserName == name));
    public Task SetPasswordHashAsync(IdentityUser user, string? hash, CancellationToken token) { user.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.PasswordHash is not null);
    public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken token) { user.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.SecurityStamp);
    public Task AddToRoleAsync(IdentityUser user, string role, CancellationToken token) { if (!Roles.ContainsKey(user.Id)) Roles[user.Id] = []; Roles[user.Id].Add(role); return Task.CompletedTask; }
    public Task RemoveFromRoleAsync(IdentityUser user, string role, CancellationToken token) { Roles.GetValueOrDefault(user.Id)?.Remove(role); return Task.CompletedTask; }
    public Task<IList<string>> GetRolesAsync(IdentityUser user, CancellationToken token) => Task.FromResult<IList<string>>(Roles.GetValueOrDefault(user.Id)?.Select(name =>
        SourcingTenderManagementSystem.Web.Security.ApplicationRoles.All.FirstOrDefault(role => role.ToUpperInvariant() == name) ?? name).ToList() ?? []);
    public Task<bool> IsInRoleAsync(IdentityUser user, string role, CancellationToken token) => Task.FromResult(Roles.GetValueOrDefault(user.Id)?.Contains(role) == true);
    public Task<IList<IdentityUser>> GetUsersInRoleAsync(string role, CancellationToken token) => Task.FromResult<IList<IdentityUser>>(Users.Values.Where(user => Roles.GetValueOrDefault(user.Id)?.Contains(role) == true).ToList());
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.LockoutEnd);
    public Task SetLockoutEndDateAsync(IdentityUser user, DateTimeOffset? end, CancellationToken token) { user.LockoutEnd = end; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(IdentityUser user, CancellationToken token) => Task.FromResult(++user.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(IdentityUser user, CancellationToken token) { user.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.LockoutEnabled);
    public Task SetLockoutEnabledAsync(IdentityUser user, bool enabled, CancellationToken token) { user.LockoutEnabled = enabled; return Task.CompletedTask; }
    public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken token) { user.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.EmailConfirmed);
    public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken token) { user.EmailConfirmed = confirmed; return Task.CompletedTask; }
    public Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken token) => Task.FromResult(Users.Values.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
    public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(IdentityUser user, string? email, CancellationToken token) { user.NormalizedEmail = email; return Task.CompletedTask; }
    public Task SetPhoneNumberAsync(IdentityUser user, string? phone, CancellationToken token) { user.PhoneNumber = phone; return Task.CompletedTask; }
    public Task<string?> GetPhoneNumberAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.PhoneNumber);
    public Task<bool> GetPhoneNumberConfirmedAsync(IdentityUser user, CancellationToken token) => Task.FromResult(user.PhoneNumberConfirmed);
    public Task SetPhoneNumberConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken token) { user.PhoneNumberConfirmed = confirmed; return Task.CompletedTask; }
}

public sealed class MemoryRoles : IRoleStore<IdentityRole>
{
    public Dictionary<string, IdentityRole> Roles { get; } = [];
    public void Dispose() { }
    public Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken token) { Roles[role.Id] = role; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken token) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken token) { Roles.Remove(role.Id); return Task.FromResult(IdentityResult.Success); }
    public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken token) => Task.FromResult(role.Id);
    public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken token) => Task.FromResult(role.Name);
    public Task SetRoleNameAsync(IdentityRole role, string? name, CancellationToken token) { role.Name = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken token) => Task.FromResult(role.NormalizedName);
    public Task SetNormalizedRoleNameAsync(IdentityRole role, string? name, CancellationToken token) { role.NormalizedName = name; return Task.CompletedTask; }
    public Task<IdentityRole?> FindByIdAsync(string id, CancellationToken token) => Task.FromResult(Roles.GetValueOrDefault(id));
    public Task<IdentityRole?> FindByNameAsync(string name, CancellationToken token) => Task.FromResult(Roles.Values.SingleOrDefault(role => role.NormalizedName == name));
}
