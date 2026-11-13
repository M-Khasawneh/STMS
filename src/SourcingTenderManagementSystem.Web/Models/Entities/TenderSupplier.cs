namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class TenderSupplier
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid SupplierProfileId { get; set; }
}
