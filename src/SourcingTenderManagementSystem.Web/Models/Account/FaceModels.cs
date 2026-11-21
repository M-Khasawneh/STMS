using System.ComponentModel.DataAnnotations;
namespace SourcingTenderManagementSystem.Web.Models.Account;

public sealed record FacePage(bool Enabled, bool Registered, string Version, DateTimeOffset? UpdatedAt, bool LoginEnabled = false);
public sealed class FaceLoginStart
{
    [Required, StringLength(256)] public string UserName { get; set; } = string.Empty;
}
public sealed class FaceOperation
{
    [Required, StringLength(16)] public string Operation { get; set; } = string.Empty;
    [StringLength(256)] public string? CurrentPassword { get; set; }
}
public sealed class FaceCapture
{
    [StringLength(2048)] public string? ReturnUrl { get; set; }
    [Required, StringLength(64)] public string Token { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = true), StringLength(32)] public string Version { get; set; } = string.Empty;
    public float[][] Samples { get; set; } = [];
}
