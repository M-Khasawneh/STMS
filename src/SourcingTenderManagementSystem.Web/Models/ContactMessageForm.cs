using System.ComponentModel.DataAnnotations;

namespace SourcingTenderManagementSystem.Web.Models;

public sealed class ContactMessageForm
{
    [Required, StringLength(100), Display(Name = "Your name")]
    public string SenderName { get; set; } = "";
    [Required, EmailAddress, StringLength(254), Display(Name = "Email address")]
    public string Email { get; set; } = "";
    [Required, StringLength(180)] public string Subject { get; set; } = "";
    [Required, StringLength(4000)] public string Message { get; set; } = "";
}
