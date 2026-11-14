using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SourcingTenderManagementSystem.Web.Data.Migrations;
public partial class EvaluationConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "TechnicalEvaluations", "FinancialEvaluations" })
        {
            migrationBuilder.AddColumn<Guid>("CommitteeMemberId", table, type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>("SubmittedAt", table, type: "datetimeoffset", nullable: true);
            migrationBuilder.CreateIndex($"IX_{table}_CommitteeMemberId_TenderId", table, new[] { "CommitteeMemberId", "TenderId" });
            migrationBuilder.CreateIndex($"IX_{table}_BidId_EvaluationCriterionId_CommitteeMemberId", table,
                new[] { "BidId", "EvaluationCriterionId", "CommitteeMemberId" }, unique: true, filter: "[CommitteeMemberId] IS NOT NULL");
            migrationBuilder.AddForeignKey($"FK_{table}_CommitteeMembers_CommitteeMemberId_TenderId", table,
                new[] { "CommitteeMemberId", "TenderId" }, "CommitteeMembers", principalColumns: new[] { "Id", "TenderId" }, onDelete: ReferentialAction.NoAction);
        }
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "TechnicalEvaluations", "FinancialEvaluations" })
        {
            migrationBuilder.DropForeignKey($"FK_{table}_CommitteeMembers_CommitteeMemberId_TenderId", table);
            migrationBuilder.DropIndex($"IX_{table}_CommitteeMemberId_TenderId", table);
            migrationBuilder.DropIndex($"IX_{table}_BidId_EvaluationCriterionId_CommitteeMemberId", table);
            migrationBuilder.DropColumn("CommitteeMemberId", table);
            migrationBuilder.DropColumn("SubmittedAt", table);
        }
    }
}
