using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SourcingTenderManagementSystem.Web.Data.Migrations;

public partial class TopCommitteeVotingRounds : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AlterColumn<DateTimeOffset>("DecidedAt", "TenderApprovals", type: "datetimeoffset", nullable: true, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset");
        m.AddColumn<DateTimeOffset>("RoundOpenedAt", "TenderApprovals", type: "datetimeoffset", nullable: true);
        m.AddColumn<DateTimeOffset>("RoundClosedAt", "TenderApprovals", type: "datetimeoffset", nullable: true);
        m.AddColumn<string>("RoundCloseReason", "TenderApprovals", type: "nvarchar(max)", maxLength: 8000, nullable: true);
        m.AddColumn<string>("RoundMemberSnapshot", "TenderApprovals", type: "nvarchar(512)", maxLength: 512, nullable: true);
        m.AddCheckConstraint("CK_TenderApprovals_TopRound", "TenderApprovals", "([RoundOpenedAt] IS NULL AND [RoundMemberSnapshot] IS NULL AND [RoundClosedAt] IS NULL AND [RoundCloseReason] IS NULL AND [DecidedAt] IS NOT NULL) OR ([RoundOpenedAt] IS NOT NULL AND [RoundMemberSnapshot] IS NOT NULL AND ISJSON([RoundMemberSnapshot]) = 1 AND [Stage] = N'TopCommittee' AND [CommitteeAssignmentId] IS NOT NULL AND (([RoundClosedAt] IS NULL AND [DecidedAt] IS NULL AND [Decision] = N'RecommendationPending' AND [RoundCloseReason] IS NULL) OR ([RoundClosedAt] IS NOT NULL AND [RoundClosedAt] >= [RoundOpenedAt] AND [RoundCloseReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RoundCloseReason]))) > 0 AND (([Decision] = N'RecommendationPending' AND [DecidedAt] IS NULL) OR ([Decision] IN (N'CommitteeApproved', N'CommitteeRejected', N'ReturnedForReview') AND [DecidedAt] IS NOT NULL AND [DecidedAt] >= [RoundOpenedAt] AND [DecidedAt] <= [RoundClosedAt])))))");
        m.CreateIndex("UX_TenderApprovals_OpenTopRound", "TenderApprovals", new[] { "TenderId", "Stage" }, unique: true, filter: "[Stage] = N'TopCommittee' AND [RoundOpenedAt] IS NOT NULL AND [RoundClosedAt] IS NULL");
        m.CreateIndex("IX_CommitteeVotes_TenderApprovalId_CommitteeMemberId", "CommitteeVotes", new[] { "TenderApprovalId", "CommitteeMemberId" }, unique: true);
        m.DropIndex("IX_TenderAwards_TenderId", "TenderAwards");
        m.CreateIndex("IX_TenderAwards_TenderId", "TenderAwards", "TenderId", unique: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        // Refuse to discard round history or manufacture a decision timestamp during rollback.
        m.Sql("IF EXISTS (SELECT 1 FROM dbo.TenderApprovals WHERE RoundOpenedAt IS NOT NULL OR DecidedAt IS NULL) THROW 51000, 'Rollback would discard voting-round history. No rollback performed.', 1;");
        m.DropCheckConstraint("CK_TenderApprovals_TopRound", "TenderApprovals");
        m.DropIndex("UX_TenderApprovals_OpenTopRound", "TenderApprovals");
        m.DropIndex("IX_CommitteeVotes_TenderApprovalId_CommitteeMemberId", "CommitteeVotes");
        m.DropIndex("IX_TenderAwards_TenderId", "TenderAwards"); m.CreateIndex("IX_TenderAwards_TenderId", "TenderAwards", "TenderId");
        m.DropColumn("RoundMemberSnapshot", "TenderApprovals"); m.DropColumn("RoundOpenedAt", "TenderApprovals"); m.DropColumn("RoundClosedAt", "TenderApprovals"); m.DropColumn("RoundCloseReason", "TenderApprovals");
        m.AlterColumn<DateTimeOffset>("DecidedAt", "TenderApprovals", type: "datetimeoffset", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
    }
}
