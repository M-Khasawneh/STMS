using System.Security.Cryptography;
using System.Text;
using SourcingTenderManagementSystem.Web.Models;
namespace SourcingTenderManagementSystem.Web.Data;
// One small, bounded lease per authenticated user/tab; polling the committed inbox avoids pre-commit pushes.
public sealed class NotificationWatchGate
{
    private readonly Dictionary<string,int> counts=new();
    public IDisposable? Enter(string userId)
    {
        lock(counts){var count=counts.GetValueOrDefault(userId);if(count>=2)return null;counts[userId]=count+1;return new Lease(()=>{lock(counts){if(counts[userId]==1)counts.Remove(userId);else counts[userId]--;}});}
    }
    private sealed class Lease(Action release):IDisposable{private Action? action=release;public void Dispose()=>Interlocked.Exchange(ref action,null)?.Invoke();}
    public static string Version(NotificationMenu menu)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{menu.Available}|{menu.Unread}|"+string.Join(";",menu.Items.Select(i=>$"{i.Id}:{i.ReadAt:O}")))));
}
