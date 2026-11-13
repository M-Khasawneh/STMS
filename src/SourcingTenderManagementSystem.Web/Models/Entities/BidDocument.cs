namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class BidDocument
{
    public Guid Id { get; set; }
    public Guid BidId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
