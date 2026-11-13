using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourcingTenderManagementSystem.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityAndProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TaxNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReviewReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProfiles", x => x.Id);
                    table.CheckConstraint("CK_SupplierProfiles_Status", "[Status] IN (N'Pending', N'Approved', N'Rejected', N'Suspended')");
                    table.ForeignKey(
                        name: "FK_SupplierProfiles_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmissionDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenders", x => x.Id);
                    table.CheckConstraint("CK_Tenders_Status", "[Status] IN (N'Draft', N'Published', N'Open', N'Closed', N'UnderEvaluation', N'AwaitingManagerReview', N'AwaitingCommitteeDecision', N'Awarded', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_Tenders_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitions", x => x.Id);
                    table.CheckConstraint("CK_PurchaseRequisitions_Status", "[Status] IN (N'Draft', N'Submitted', N'UnderReview', N'Approved', N'Rejected', N'Returned', N'ConvertedToTender')");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    StorageReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDocuments_SupplierProfiles_SupplierProfileId",
                        column: x => x.SupplierProfileId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AiBidAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InputReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    AnalysisJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiBidAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiBidAnalyses_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiBidAnalyses_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommitteeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeAssignments", x => x.Id);
                    table.UniqueConstraint("AK_CommitteeAssignments_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.CheckConstraint("CK_CommitteeAssignments_Kind", "[Kind] IN (N'Evaluation', N'TopCommittee')");
                    table.ForeignKey(
                        name: "FK_CommitteeAssignments_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EvaluationCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCriteria", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationCriteria_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.CheckConstraint("CK_EvaluationCriteria_Kind", "[Kind] IN (N'Technical', N'Financial')");
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderSuppliers", x => x.Id);
                    table.UniqueConstraint("AK_TenderSuppliers_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.ForeignKey(
                        name: "FK_TenderSuppliers_SupplierProfiles_SupplierProfileId",
                        column: x => x.SupplierProfileId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderSuppliers_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitionReviews", x => x.Id);
                    table.CheckConstraint("CK_PurchaseRequisitionReviews_Outcome", "[Outcome] IN (N'Draft', N'Submitted', N'UnderReview', N'Approved', N'Rejected', N'Returned', N'ConvertedToTender')");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionReviews_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionReviews_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderPurchaseRequisitions",
                columns: table => new
                {
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderPurchaseRequisitions", x => new { x.TenderId, x.PurchaseRequisitionId });
                    table.ForeignKey(
                        name: "FK_TenderPurchaseRequisitions_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderPurchaseRequisitions_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMembers", x => x.Id);
                    table.UniqueConstraint("AK_CommitteeMembers_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_CommitteeAssignments_CommitteeAssignmentId_TenderId",
                        columns: x => new { x.CommitteeAssignmentId, x.TenderId },
                        principalTable: "CommitteeAssignments",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeliveryTimeframe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OfferValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.UniqueConstraint("AK_Bids_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.CheckConstraint("CK_Bids_Status", "[Status] IN (N'Draft', N'Submitted', N'UnderReview', N'Compliant', N'NonCompliant', N'Accepted', N'Rejected', N'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_Bids_TenderSuppliers_TenderSupplierId_TenderId",
                        columns: x => new { x.TenderSupplierId, x.TenderId },
                        principalTable: "TenderSuppliers",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_Bids_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.CheckConstraint("CK_Invitations_Status", "[Status] IN (N'Pending', N'Accepted', N'Declined', N'Expired')");
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invitations_TenderSuppliers_TenderSupplierId",
                        column: x => x.TenderSupplierId,
                        principalTable: "TenderSuppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BidCostComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidCostComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidCostComponents_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BidDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    StorageReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidDocuments_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BidItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OfferedQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidItems_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BidItems_PurchaseRequisitionItems_PurchaseRequisitionItemId",
                        column: x => x.PurchaseRequisitionItemId,
                        principalTable: "PurchaseRequisitionItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClarificationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClarificationRequests", x => x.Id);
                    table.CheckConstraint("CK_ClarificationRequests_OneRelatedRecord", "(CASE WHEN [PurchaseRequisitionId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TenderId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [BidId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_ClarificationRequests_Status", "[Status] IN (N'Pending', N'Answered', N'Resolved')");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ComplianceReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reasons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceReviews", x => x.Id);
                    table.CheckConstraint("CK_ComplianceReviews_NonCompliantReason", "[Outcome] <> N'NonCompliant' OR ([Reasons] IS NOT NULL AND LEN(LTRIM(RTRIM([Reasons]))) > 0)");
                    table.CheckConstraint("CK_ComplianceReviews_Outcome", "[Outcome] IN (N'Compliant', N'NonCompliant', N'PendingClarification')");
                    table.ForeignKey(
                        name: "FK_ComplianceReviews_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceReviews_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EvaluationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecommendedBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TechnicalSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinancialSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Observations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NonCompliance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comparison = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalResult = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReports", x => x.Id);
                    table.UniqueConstraint("AK_EvaluationReports_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.ForeignKey(
                        name: "FK_EvaluationReports_AspNetUsers_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvaluationReports_Bids_RecommendedBidId_TenderId",
                        columns: x => new { x.RecommendedBidId, x.TenderId },
                        principalTable: "Bids",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_EvaluationReports_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FinancialEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationCriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EvaluatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialEvaluations_AspNetUsers_EvaluatorUserId",
                        column: x => x.EvaluatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialEvaluations_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialEvaluations_Bids_BidId_TenderId",
                        columns: x => new { x.BidId, x.TenderId },
                        principalTable: "Bids",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_FinancialEvaluations_EvaluationCriteria_EvaluationCriterionId_TenderId",
                        columns: x => new { x.EvaluationCriterionId, x.TenderId },
                        principalTable: "EvaluationCriteria",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_FinancialEvaluations_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnicalEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationCriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EvaluatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NonCompliance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluations_AspNetUsers_EvaluatorUserId",
                        column: x => x.EvaluatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluations_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluations_Bids_BidId_TenderId",
                        columns: x => new { x.BidId, x.TenderId },
                        principalTable: "Bids",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluations_EvaluationCriteria_EvaluationCriterionId_TenderId",
                        columns: x => new { x.EvaluationCriterionId, x.TenderId },
                        principalTable: "EvaluationCriteria",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_TechnicalEvaluations_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClarificationResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClarificationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClarificationResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClarificationResponses_AspNetUsers_ResponderUserId",
                        column: x => x.ResponderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationResponses_ClarificationRequests_ClarificationRequestId",
                        column: x => x.ClarificationRequestId,
                        principalTable: "ClarificationRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CommitteeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderApprovals", x => x.Id);
                    table.UniqueConstraint("AK_TenderApprovals_Id_TenderId", x => new { x.Id, x.TenderId });
                    table.CheckConstraint("CK_TenderApprovals_Decision", "[Decision] IN (N'RecommendationPending', N'ManagerApproved', N'ManagerRejected', N'ReturnedForReview', N'CommitteeApproved', N'CommitteeRejected')");
                    table.CheckConstraint("CK_TenderApprovals_Stage", "[Stage] IN (N'Manager', N'TopCommittee')");
                    table.ForeignKey(
                        name: "FK_TenderApprovals_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderApprovals_CommitteeAssignments_CommitteeAssignmentId_TenderId",
                        columns: x => new { x.CommitteeAssignmentId, x.TenderId },
                        principalTable: "CommitteeAssignments",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_TenderApprovals_EvaluationReports_EvaluationReportId_TenderId",
                        columns: x => new { x.EvaluationReportId, x.TenderId },
                        principalTable: "EvaluationReports",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_TenderApprovals_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommitteeVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommitteeMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VotedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeVotes_CommitteeMembers_CommitteeMemberId_TenderId",
                        columns: x => new { x.CommitteeMemberId, x.TenderId },
                        principalTable: "CommitteeMembers",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_CommitteeVotes_TenderApprovals_TenderApprovalId_TenderId",
                        columns: x => new { x.TenderApprovalId, x.TenderId },
                        principalTable: "TenderApprovals",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_CommitteeVotes_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenderAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AwardedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ContractReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderAwards_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenderAwards_Bids_BidId_TenderId",
                        columns: x => new { x.BidId, x.TenderId },
                        principalTable: "Bids",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_TenderAwards_TenderApprovals_TenderApprovalId_TenderId",
                        columns: x => new { x.TenderApprovalId, x.TenderId },
                        principalTable: "TenderApprovals",
                        principalColumns: new[] { "Id", "TenderId" });
                    table.ForeignKey(
                        name: "FK_TenderAwards_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiBidAnalyses_RequestedByUserId",
                table: "AiBidAnalyses",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiBidAnalyses_TenderId",
                table: "AiBidAnalyses",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RecordType_RecordId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "RecordType", "RecordId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BidCostComponents_BidId",
                table: "BidCostComponents",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidDocuments_BidId",
                table: "BidDocuments",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidItems_BidId",
                table: "BidItems",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidItems_PurchaseRequisitionItemId",
                table: "BidItems",
                column: "PurchaseRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TenderId",
                table: "Bids",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TenderSupplierId_TenderId",
                table: "Bids",
                columns: new[] { "TenderSupplierId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_BidId",
                table: "ClarificationRequests",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_PurchaseRequisitionId",
                table: "ClarificationRequests",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_RecipientUserId",
                table: "ClarificationRequests",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_SenderUserId",
                table: "ClarificationRequests",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_TenderId",
                table: "ClarificationRequests",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationResponses_ClarificationRequestId",
                table: "ClarificationResponses",
                column: "ClarificationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationResponses_ResponderUserId",
                table: "ClarificationResponses",
                column: "ResponderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeAssignments_TenderId",
                table: "CommitteeAssignments",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_CommitteeAssignmentId_TenderId",
                table: "CommitteeMembers",
                columns: new[] { "CommitteeAssignmentId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_TenderId",
                table: "CommitteeMembers",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_UserId",
                table: "CommitteeMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_CommitteeMemberId_TenderId",
                table: "CommitteeVotes",
                columns: new[] { "CommitteeMemberId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_TenderApprovalId_TenderId",
                table: "CommitteeVotes",
                columns: new[] { "TenderApprovalId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_TenderId",
                table: "CommitteeVotes",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceReviews_BidId",
                table: "ComplianceReviews",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceReviews_ReviewerUserId",
                table: "ComplianceReviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_ApprovedByUserId",
                table: "EvaluationCriteria",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_TenderId",
                table: "EvaluationCriteria",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReports_PreparedByUserId",
                table: "EvaluationReports",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReports_RecommendedBidId_TenderId",
                table: "EvaluationReports",
                columns: new[] { "RecommendedBidId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReports_TenderId",
                table: "EvaluationReports",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluations_BidId_TenderId",
                table: "FinancialEvaluations",
                columns: new[] { "BidId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluations_EvaluationCriterionId_TenderId",
                table: "FinancialEvaluations",
                columns: new[] { "EvaluationCriterionId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluations_EvaluatorUserId",
                table: "FinancialEvaluations",
                column: "EvaluatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluations_RecordedByUserId",
                table: "FinancialEvaluations",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvaluations_TenderId",
                table: "FinancialEvaluations",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SentByUserId",
                table: "Invitations",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenderSupplierId",
                table: "Invitations",
                column: "TenderSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_BidId",
                table: "Notifications",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PurchaseRequisitionId",
                table: "Notifications",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenderId",
                table: "Notifications",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_PurchaseRequisitionId",
                table: "PurchaseRequisitionItems",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionReviews_PurchaseRequisitionId",
                table: "PurchaseRequisitionReviews",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionReviews_ReviewerUserId",
                table: "PurchaseRequisitionReviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_CreatedByUserId",
                table: "PurchaseRequisitions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_DepartmentId_Status",
                table: "PurchaseRequisitions",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_SupplierProfileId",
                table: "SupplierDocuments",
                column: "SupplierProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProfiles_ReviewedByUserId",
                table: "SupplierProfiles",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProfiles_UserId",
                table: "SupplierProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluations_BidId_TenderId",
                table: "TechnicalEvaluations",
                columns: new[] { "BidId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluations_EvaluationCriterionId_TenderId",
                table: "TechnicalEvaluations",
                columns: new[] { "EvaluationCriterionId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluations_EvaluatorUserId",
                table: "TechnicalEvaluations",
                column: "EvaluatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluations_RecordedByUserId",
                table: "TechnicalEvaluations",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalEvaluations_TenderId",
                table: "TechnicalEvaluations",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderApprovals_ActorUserId",
                table: "TenderApprovals",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderApprovals_CommitteeAssignmentId_TenderId",
                table: "TenderApprovals",
                columns: new[] { "CommitteeAssignmentId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenderApprovals_EvaluationReportId_TenderId",
                table: "TenderApprovals",
                columns: new[] { "EvaluationReportId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenderApprovals_TenderId",
                table: "TenderApprovals",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_BidId_TenderId",
                table: "TenderAwards",
                columns: new[] { "BidId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_RecordedByUserId",
                table: "TenderAwards",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_TenderApprovalId_TenderId",
                table: "TenderAwards",
                columns: new[] { "TenderApprovalId", "TenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwards_TenderId",
                table: "TenderAwards",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderPurchaseRequisitions_PurchaseRequisitionId",
                table: "TenderPurchaseRequisitions",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_CreatedByUserId",
                table: "Tenders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_Status_SubmissionDeadline",
                table: "Tenders",
                columns: new[] { "Status", "SubmissionDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_TenderSuppliers_SupplierProfileId",
                table: "TenderSuppliers",
                column: "SupplierProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderSuppliers_TenderId",
                table: "TenderSuppliers",
                column: "TenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiBidAnalyses");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BidCostComponents");

            migrationBuilder.DropTable(
                name: "BidDocuments");

            migrationBuilder.DropTable(
                name: "BidItems");

            migrationBuilder.DropTable(
                name: "ClarificationResponses");

            migrationBuilder.DropTable(
                name: "CommitteeVotes");

            migrationBuilder.DropTable(
                name: "ComplianceReviews");

            migrationBuilder.DropTable(
                name: "FinancialEvaluations");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitionReviews");

            migrationBuilder.DropTable(
                name: "SupplierDocuments");

            migrationBuilder.DropTable(
                name: "TechnicalEvaluations");

            migrationBuilder.DropTable(
                name: "TenderAwards");

            migrationBuilder.DropTable(
                name: "TenderPurchaseRequisitions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitionItems");

            migrationBuilder.DropTable(
                name: "ClarificationRequests");

            migrationBuilder.DropTable(
                name: "CommitteeMembers");

            migrationBuilder.DropTable(
                name: "EvaluationCriteria");

            migrationBuilder.DropTable(
                name: "TenderApprovals");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitions");

            migrationBuilder.DropTable(
                name: "CommitteeAssignments");

            migrationBuilder.DropTable(
                name: "EvaluationReports");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "TenderSuppliers");

            migrationBuilder.DropTable(
                name: "SupplierProfiles");

            migrationBuilder.DropTable(
                name: "Tenders");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
