using System.ComponentModel.DataAnnotations;
namespace SourcingTenderManagementSystem.Web.Models.Account;

public sealed record FacePage(bool Enabled, bool Registered, string Version, DateTimeOffset? UpdatedAt);
public sealed class FaceOperation
{
    [Required, StringLength(16)] public string Operation { get; set; } = string.Empty;
    [StringLength(256)] public string? CurrentPassword { get; set; }
}
public sealed class FaceCapture
{
    [Required, StringLength(64)] public string Token { get; set; } = string.Empty;
    [StringLength(32)] public string Version { get; set; } = string.Empty;
    public float[][] Samples { get; set; } = [];
}
