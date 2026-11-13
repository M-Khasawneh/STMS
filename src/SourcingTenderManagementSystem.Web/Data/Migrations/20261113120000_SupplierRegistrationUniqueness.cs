using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SourcingTenderManagementSystem.Web.Data.Migrations;
public partial class SupplierRegistrationUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SupplierProfiles_UserId", table: "SupplierProfiles");
        migrationBuilder.CreateIndex(name: "IX_SupplierProfiles_UserId", table: "SupplierProfiles", column: "UserId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_SupplierProfiles_RegistrationNumber", table: "SupplierProfiles", column: "RegistrationNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_SupplierProfiles_TaxNumber", table: "SupplierProfiles", column: "TaxNumber", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SupplierProfiles_UserId", table: "SupplierProfiles");
        migrationBuilder.DropIndex(name: "IX_SupplierProfiles_RegistrationNumber", table: "SupplierProfiles");
        migrationBuilder.DropIndex(name: "IX_SupplierProfiles_TaxNumber", table: "SupplierProfiles");
        migrationBuilder.CreateIndex(name: "IX_SupplierProfiles_UserId", table: "SupplierProfiles", column: "UserId");
    }
}
