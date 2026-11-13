using System.ComponentModel.DataAnnotations;

namespace SourcingTenderManagementSystem.Web.Models.Account;

public sealed class ProfileContactForm
{
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Phone, Display(Name = "Phone number")] public string? PhoneNumber { get; set; }
}
public sealed class ProfilePasswordForm
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "New password")] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm new password")] public string ConfirmPassword { get; set; } = "";
}
public sealed class ProfilePage
{
    public string UserName { get; init; } = "";
    public ProfileContactForm Contact { get; set; } = new();
    public ProfilePasswordForm Password { get; set; } = new();
}
