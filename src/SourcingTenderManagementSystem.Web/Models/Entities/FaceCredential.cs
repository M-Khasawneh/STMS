namespace SourcingTenderManagementSystem.Web.Models.Entities;

// A camera template is not an authentication credential. Device sign-in remains WebAuthn.
public sealed class FaceCredential
{
    public string UserId { get; set; } = string.Empty;
    public byte[] ProtectedTemplate { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
