namespace SourcingTenderManagementSystem.Web.Models.Entities;

// The encrypted payload includes the owner's explicit camera-login opt-in (legacy templates default off).
public sealed class FaceCredential
{
    public string UserId { get; set; } = string.Empty;
    public byte[] ProtectedTemplate { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
