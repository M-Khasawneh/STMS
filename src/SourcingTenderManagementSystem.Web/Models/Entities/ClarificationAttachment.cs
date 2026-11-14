namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class ClarificationAttachment
{
    public Guid Id { get; set; }
    public Guid ClarificationResponseId { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
}
