using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace SourcingTenderManagementSystem.Web.Data.Migrations;

public partial class ClarificationAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClarificationAttachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClarificationResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                StorageReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Size = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClarificationAttachments", x => x.Id);
                table.CheckConstraint("CK_ClarificationAttachments_Size", "[Size] > 0 AND [Size] <= 5242880");
                table.ForeignKey("FK_ClarificationAttachments_ClarificationResponses_ClarificationResponseId",
                    x => x.ClarificationResponseId, "ClarificationResponses", "Id", onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateIndex("IX_ClarificationAttachments_ClarificationResponseId", "ClarificationAttachments", "ClarificationResponseId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("ClarificationAttachments");
}
