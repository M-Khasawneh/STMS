using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourcingTenderManagementSystem.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeDepartmentAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only the explicitly approved existing account may be backfilled.
            // Stop instead of assigning any newly discovered employee arbitrarily.
            migrationBuilder.Sql("""
                DECLARE @UserCount bigint = (SELECT COUNT_BIG(*) FROM [dbo].[AspNetUsers] WITH (TABLOCKX, HOLDLOCK));
                IF @UserCount <> 1 OR NOT EXISTS (
                    SELECT 1 FROM [dbo].[AspNetUsers] u
                    JOIN [dbo].[AspNetUserRoles] ur ON ur.UserId=u.Id
                    JOIN [dbo].[AspNetRoles] r ON r.Id=ur.RoleId
                    WHERE u.UserName=N'admin' AND u.NormalizedUserName=N'ADMIN' AND r.Name=N'Admin')
                    THROW 51000, 'Employee department migration requires the reviewed first Admin account; inspect existing users before proceeding.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[SupplierProfiles]) OR EXISTS (
                    SELECT 1 FROM [dbo].[AspNetUserRoles] ur JOIN [dbo].[AspNetRoles] r ON r.Id=ur.RoleId WHERE r.Name=N'Supplier')
                    THROW 51001, 'Unexpected supplier account data requires review before department assignment.', 1;
                IF (SELECT COUNT(*) FROM [dbo].[Departments] WITH (UPDLOCK, HOLDLOCK) WHERE Name=N'Administration') > 1
                    OR EXISTS (SELECT 1 FROM [dbo].[Departments] WHERE Name=N'Administration'
                        AND Name COLLATE Latin1_General_100_BIN2 <> N'Administration')
                    THROW 51002, 'Conflicting Administration departments require review; no record will be overwritten.', 1;
                """);
            migrationBuilder.CreateTable(
                name: "EmployeeDepartments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDepartments", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_EmployeeDepartments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDepartments_DepartmentId",
                table: "EmployeeDepartments",
                column: "DepartmentId");

            migrationBuilder.Sql("""
                DECLARE @DepartmentId uniqueidentifier = (SELECT Id FROM [dbo].[Departments] WHERE Name=N'Administration');
                IF @DepartmentId IS NULL
                BEGIN
                    SET @DepartmentId=NEWID();
                    INSERT INTO [dbo].[Departments] (Id,Name) VALUES (@DepartmentId,N'Administration');
                END;
                INSERT INTO [dbo].[EmployeeDepartments] (UserId,DepartmentId)
                    SELECT Id,@DepartmentId FROM [dbo].[AspNetUsers] WHERE NormalizedUserName=N'ADMIN' AND UserName=N'admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("THROW 51003, 'Removing mandatory employee department assignments requires a separately reviewed data migration.', 1;");
        }
    }
}
