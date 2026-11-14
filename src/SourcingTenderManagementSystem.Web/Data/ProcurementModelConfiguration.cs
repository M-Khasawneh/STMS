using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

internal static class ProcurementModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        // No approval authority, scoring formula, bid limit, quorum, or workflow
        // transition is inferred from these persistence mappings.
        modelBuilder.Entity<Department>().ToTable("Departments");
        modelBuilder.Entity<EmployeeDepartment>(entity =>
        {
            entity.ToTable("EmployeeDepartments");
            entity.HasKey(item => item.UserId);
            entity.HasOne<IdentityUser>().WithOne().HasForeignKey<EmployeeDepartment>(item => item.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Department>().WithMany().HasForeignKey(item => item.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<SupplierProfile>().ToTable("SupplierProfiles");
        modelBuilder.Entity<SupplierDocument>().ToTable("SupplierDocuments");
        modelBuilder.Entity<PurchaseRequisition>().ToTable("PurchaseRequisitions");
        modelBuilder.Entity<PurchaseRequisitionItem>().ToTable("PurchaseRequisitionItems");
        modelBuilder.Entity<PurchaseRequisitionReview>().ToTable("PurchaseRequisitionReviews");
        modelBuilder.Entity<Tender>().ToTable("Tenders");
        modelBuilder.Entity<TenderSupplier>().ToTable("TenderSuppliers");
        modelBuilder.Entity<Invitation>().ToTable("Invitations");
        modelBuilder.Entity<Bid>().ToTable("Bids");
        modelBuilder.Entity<BidItem>().ToTable("BidItems");
        modelBuilder.Entity<BidDocument>().ToTable("BidDocuments");
        modelBuilder.Entity<EvaluationCriterion>().ToTable("EvaluationCriteria");
        modelBuilder.Entity<TechnicalEvaluation>().ToTable("TechnicalEvaluations");
        modelBuilder.Entity<FinancialEvaluation>().ToTable("FinancialEvaluations");
        modelBuilder.Entity<BidCostComponent>().ToTable("BidCostComponents");
        modelBuilder.Entity<EvaluationReport>().ToTable("EvaluationReports");
        modelBuilder.Entity<TenderApproval>().ToTable("TenderApprovals");
        modelBuilder.Entity<CommitteeAssignment>().ToTable("CommitteeAssignments");
        modelBuilder.Entity<CommitteeMember>().ToTable("CommitteeMembers");
        modelBuilder.Entity<CommitteeVote>().ToTable("CommitteeVotes");
        modelBuilder.Entity<ComplianceReview>().ToTable("ComplianceReviews");
        modelBuilder.Entity<ClarificationRequest>().ToTable("ClarificationRequests");
        modelBuilder.Entity<ClarificationResponse>().ToTable("ClarificationResponses");
        modelBuilder.Entity<Notification>().ToTable("Notifications");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
        modelBuilder.Entity<AiBidAnalysis>().ToTable("AiBidAnalyses");
        modelBuilder.Entity<TenderAward>().ToTable("TenderAwards");

        modelBuilder.Entity<TenderPurchaseRequisition>(entity =>
        {
            entity.ToTable("TenderPurchaseRequisitions");
            entity.HasKey(x => new { x.TenderId, x.PurchaseRequisitionId });
            entity.HasIndex(x => x.TenderId).IsUnique();
            entity.HasIndex(x => x.PurchaseRequisitionId).IsUnique();
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<PurchaseRequisition>().WithMany().HasForeignKey(x => x.PurchaseRequisitionId).OnDelete(DeleteBehavior.NoAction);
        });

        // Scope keys prevent cross-tender references without inventing bid/award limits.
        modelBuilder.Entity<TenderSupplier>().HasAlternateKey(x => new { x.Id, x.TenderId });
        modelBuilder.Entity<Bid>().HasAlternateKey(x => new { x.Id, x.TenderId });
        modelBuilder.Entity<EvaluationCriterion>().HasAlternateKey(x => new { x.Id, x.TenderId });
        modelBuilder.Entity<EvaluationReport>().HasAlternateKey(x => new { x.Id, x.TenderId });
        modelBuilder.Entity<CommitteeAssignment>().HasAlternateKey(x => new { x.Id, x.TenderId });
        modelBuilder.Entity<TenderApproval>().HasAlternateKey(x => new { x.Id, x.TenderId });
        modelBuilder.Entity<CommitteeMember>().HasAlternateKey(x => new { x.Id, x.TenderId });

        modelBuilder.Entity<SupplierProfile>(entity =>
        {
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.RegistrationNumber).IsUnique();
            entity.HasIndex(x => x.TaxNumber).IsUnique();
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SupplierDocument>(entity =>
        {
            entity.HasOne<SupplierProfile>().WithMany().HasForeignKey(x => x.SupplierProfileId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PurchaseRequisition>(entity =>
        {
            entity.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PurchaseRequisitionItem>(entity =>
        {
            entity.HasOne<PurchaseRequisition>().WithMany().HasForeignKey(x => x.PurchaseRequisitionId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PurchaseRequisitionReview>(entity =>
        {
            entity.HasOne<PurchaseRequisition>().WithMany().HasForeignKey(x => x.PurchaseRequisitionId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Tender>(entity =>
        {
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TenderSupplier>(entity =>
        {
            entity.HasIndex(x => x.TenderId);
            entity.HasIndex(x => new { x.TenderId, x.SupplierProfileId }).IsUnique();
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SupplierProfile>().WithMany().HasForeignKey(x => x.SupplierProfileId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasIndex(x => x.TenderSupplierId).IsUnique();
            entity.HasOne<TenderSupplier>().WithMany().HasForeignKey(x => x.TenderSupplierId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.SentByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Bid>(entity =>
        {
            entity.HasOne<TenderSupplier>().WithMany()
                .HasForeignKey(x => new { x.TenderSupplierId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BidItem>(entity =>
        {
            entity.HasOne<Bid>().WithMany().HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<PurchaseRequisitionItem>().WithMany().HasForeignKey(x => x.PurchaseRequisitionItemId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BidDocument>(entity =>
        {
            entity.HasOne<Bid>().WithMany().HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EvaluationCriterion>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TechnicalEvaluation>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Bid>().WithMany()
                .HasForeignKey(x => new { x.BidId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<EvaluationCriterion>().WithMany()
                .HasForeignKey(x => new { x.EvaluationCriterionId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.EvaluatorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<FinancialEvaluation>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Bid>().WithMany()
                .HasForeignKey(x => new { x.BidId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<EvaluationCriterion>().WithMany()
                .HasForeignKey(x => new { x.EvaluationCriterionId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.EvaluatorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BidCostComponent>(entity =>
        {
            entity.HasOne<Bid>().WithMany().HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EvaluationReport>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.PreparedByUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Bid>().WithMany()
                .HasForeignKey(x => new { x.RecommendedBidId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TenderApproval>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<EvaluationReport>().WithMany()
                .HasForeignKey(x => new { x.EvaluationReportId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CommitteeAssignment>().WithMany()
                .HasForeignKey(x => new { x.CommitteeAssignmentId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CommitteeAssignment>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CommitteeMember>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CommitteeAssignment>().WithMany()
                .HasForeignKey(x => new { x.CommitteeAssignmentId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CommitteeVote>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TenderApproval>().WithMany()
                .HasForeignKey(x => new { x.TenderApprovalId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<CommitteeMember>().WithMany()
                .HasForeignKey(x => new { x.CommitteeMemberId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ComplianceReview>(entity =>
        {
            entity.HasOne<Bid>().WithMany().HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ClarificationRequest>(entity =>
        {
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<PurchaseRequisition>().WithMany().HasForeignKey(x => x.PurchaseRequisitionId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Bid>().WithMany().HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ClarificationResponse>(entity =>
        {
            entity.HasOne<ClarificationRequest>().WithMany().HasForeignKey(x => x.ClarificationRequestId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ResponderUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<PurchaseRequisition>().WithMany().HasForeignKey(x => x.PurchaseRequisitionId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Bid>().WithMany().HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AiBidAnalysis>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TenderAward>(entity =>
        {
            entity.HasOne<Tender>().WithMany().HasForeignKey(x => x.TenderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Bid>().WithMany()
                .HasForeignKey(x => new { x.BidId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TenderApproval>().WithMany()
                .HasForeignKey(x => new { x.TenderApprovalId, x.TenderId })
                .HasPrincipalKey(x => new { x.Id, x.TenderId }).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ComplianceReview>().ToTable("ComplianceReviews", table =>
            table.HasCheckConstraint("CK_ComplianceReviews_NonCompliantReason",
                "[Outcome] <> N'NonCompliant' OR ([Reasons] IS NOT NULL AND LEN(LTRIM(RTRIM([Reasons]))) > 0)"));
        modelBuilder.Entity<ClarificationRequest>().ToTable("ClarificationRequests", table =>
            table.HasCheckConstraint("CK_ClarificationRequests_OneRelatedRecord",
                "(CASE WHEN [PurchaseRequisitionId] IS NULL THEN 0 ELSE 1 END + " +
                "CASE WHEN [TenderId] IS NULL THEN 0 ELSE 1 END + " +
                "CASE WHEN [BidId] IS NULL THEN 0 ELSE 1 END) = 1"));

        modelBuilder.Entity<Notification>().HasIndex(x => new { x.RecipientUserId, x.CreatedAt });
        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.RecordType, x.RecordId, x.OccurredAt });
        modelBuilder.Entity<PurchaseRequisition>().HasIndex(x => new { x.DepartmentId, x.Status });
        modelBuilder.Entity<Tender>().HasIndex(x => new { x.Status, x.SubmissionDeadline });

        modelBuilder.Entity<ClarificationAttachment>(entity =>
        {
            entity.ToTable("ClarificationAttachments", table =>
                table.HasCheckConstraint("CK_ClarificationAttachments_Size", "[Size] > 0 AND [Size] <= 5242880"));
            entity.HasOne<ClarificationResponse>().WithMany().HasForeignKey(x => x.ClarificationResponseId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(x => x.Sha256).HasMaxLength(64);
        });
        ConfigureStorage(modelBuilder);
        foreach (var type in new[] { typeof(TechnicalEvaluation), typeof(FinancialEvaluation) })
        {
            var entity = modelBuilder.Entity(type);
            entity.HasOne(typeof(CommitteeMember)).WithMany().HasForeignKey("CommitteeMemberId", "TenderId")
                .HasPrincipalKey("Id", "TenderId").OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex("BidId", "EvaluationCriterionId", "CommitteeMemberId").IsUnique().HasFilter("[CommitteeMemberId] IS NOT NULL");
        }
    }

    private static void ConfigureStorage(ModelBuilder modelBuilder)
    {
        var domainTypes = modelBuilder.Model.GetEntityTypes()
            .Where(x => x.ClrType.Namespace == typeof(Tender).Namespace).ToArray();
        foreach (var entityType in domainTypes)
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            foreach (var property in entityType.GetProperties().ToArray())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type == typeof(decimal))
                {
                    entity.Property(property.Name).HasPrecision(18, 6);
                }
                else if (type == typeof(string))
                {
                    var length = property.Name switch
                    {
                        "UserId" => 450,
                        _ when property.Name.EndsWith("UserId", StringComparison.Ordinal) => 450,
                        "Name" or "Title" or "Subject" or "CompanyName" or "ContactName" or
                            "ContactEmail" or "ContentType" or "ModelName" => 256,
                        "TaxNumber" or "RegistrationNumber" or "Position" or "CostType" or "RecordType" => 128,
                        "ContactPhone" or "Priority" => 64,
                        "Currency" => 16,
                        "OriginalName" or "ContractReference" => 512,
                        "StorageReference" or "InputReference" => 2048,
                        "RecordId" => 450,
                        _ => 0
                    };
                    if (length > 0)
                        entity.Property(property.Name).HasMaxLength(length);
                }
                else if (type.IsEnum)
                {
                    entity.Property(property.Name).HasConversion<string>().HasMaxLength(64);
                    var allowed = string.Join(", ", Enum.GetNames(type).Select(x => $"N'{x}'"));
                    entity.ToTable(entityType.GetTableName()!, table =>
                        table.HasCheckConstraint($"CK_{entityType.GetTableName()}_{property.Name}",
                            $"[{property.Name}] IN ({allowed})"));
                }
            }
        }
    }
}
