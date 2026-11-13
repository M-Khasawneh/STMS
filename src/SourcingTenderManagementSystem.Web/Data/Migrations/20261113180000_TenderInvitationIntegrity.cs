using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SourcingTenderManagementSystem.Web.Data.Migrations;
public partial class TenderInvitationIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_TenderPurchaseRequisitions_PurchaseRequisitionId", "TenderPurchaseRequisitions");
        migrationBuilder.DropIndex("IX_Invitations_TenderSupplierId", "Invitations");
        migrationBuilder.CreateIndex("IX_TenderPurchaseRequisitions_PurchaseRequisitionId", "TenderPurchaseRequisitions", "PurchaseRequisitionId", unique: true);
        migrationBuilder.CreateIndex("IX_TenderPurchaseRequisitions_TenderId", "TenderPurchaseRequisitions", "TenderId", unique: true);
        migrationBuilder.CreateIndex("IX_TenderSuppliers_TenderId_SupplierProfileId", "TenderSuppliers", new[] { "TenderId", "SupplierProfileId" }, unique: true);
        migrationBuilder.CreateIndex("IX_Invitations_TenderSupplierId", "Invitations", "TenderSupplierId", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_TenderPurchaseRequisitions_PurchaseRequisitionId", "TenderPurchaseRequisitions");
        migrationBuilder.DropIndex("IX_TenderPurchaseRequisitions_TenderId", "TenderPurchaseRequisitions");
        migrationBuilder.DropIndex("IX_TenderSuppliers_TenderId_SupplierProfileId", "TenderSuppliers");
        migrationBuilder.DropIndex("IX_Invitations_TenderSupplierId", "Invitations");
        migrationBuilder.CreateIndex("IX_TenderPurchaseRequisitions_PurchaseRequisitionId", "TenderPurchaseRequisitions", "PurchaseRequisitionId");
        migrationBuilder.CreateIndex("IX_Invitations_TenderSupplierId", "Invitations", "TenderSupplierId");
    }
}
