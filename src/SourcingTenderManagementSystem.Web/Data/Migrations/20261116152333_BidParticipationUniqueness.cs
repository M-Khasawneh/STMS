using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourcingTenderManagementSystem.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class BidParticipationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.Bids GROUP BY TenderSupplierId HAVING COUNT_BIG(*) > 1) THROW 51000, 'Duplicate bid participation records require review. No data was changed.', 1;");
            migrationBuilder.CreateIndex(
                name: "IX_Bids_TenderSupplierId",
                table: "Bids",
                column: "TenderSupplierId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bids_TenderSupplierId",
                table: "Bids");
        }
    }
}
