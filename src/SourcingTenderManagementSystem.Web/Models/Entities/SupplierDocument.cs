namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class SupplierDocument
{
    public Guid Id { get; set; }
    public Guid SupplierProfileId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
