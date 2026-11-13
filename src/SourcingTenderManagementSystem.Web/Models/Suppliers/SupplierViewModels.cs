using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Suppliers;

public sealed class SupplierRegistrationViewModel
{
    [Required, StringLength(256), Display(Name = "Company name")] public string CompanyName { get; set; } = "";
    [Required, StringLength(128), Display(Name = "Company registration number")] public string RegistrationNumber { get; set; } = "";
    [Required, StringLength(128), Display(Name = "Tax registration number")] public string TaxNumber { get; set; } = "";
    [Required, StringLength(256), Display(Name = "Contact name")] public string ContactName { get; set; } = "";
    [Required, EmailAddress, StringLength(256), Display(Name = "Contact email")] public string ContactEmail { get; set; } = "";
    [Required, Phone, StringLength(64), Display(Name = "Contact phone")] public string ContactPhone { get; set; } = "";
    [Required, StringLength(256), Display(Name = "Username")] public string UserName { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
    [Required, Display(Name = "Company Registration Certificate")] public IFormFile? CompanyCertificate { get; set; }
    [Required, Display(Name = "Business / Trade License")] public IFormFile? TradeLicense { get; set; }
    [Required, Display(Name = "Tax Registration Certificate")] public IFormFile? TaxCertificate { get; set; }
}

public sealed class SupplierReviewViewModel
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public SupplierProfile? Supplier { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public IReadOnlyList<SupplierDocument> Documents { get; set; } = [];
    [Required] public SupplierStatus? Decision { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
    [Required] public SupplierStatus? ExpectedStatus { get; set; }
}
