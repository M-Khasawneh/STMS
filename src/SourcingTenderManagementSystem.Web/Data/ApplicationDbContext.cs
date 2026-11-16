using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EmployeeDepartment> EmployeeDepartments => Set<EmployeeDepartment>();
    public DbSet<SupplierProfile> SupplierProfiles => Set<SupplierProfile>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems => Set<PurchaseRequisitionItem>();
    public DbSet<PurchaseRequisitionReview> PurchaseRequisitionReviews => Set<PurchaseRequisitionReview>();
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderSupplier> TenderSuppliers => Set<TenderSupplier>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<BidItem> BidItems => Set<BidItem>();
    public DbSet<BidDocument> BidDocuments => Set<BidDocument>();
    public DbSet<EvaluationCriterion> EvaluationCriteria => Set<EvaluationCriterion>();
    public DbSet<TechnicalEvaluation> TechnicalEvaluations => Set<TechnicalEvaluation>();
    public DbSet<FinancialEvaluation> FinancialEvaluations => Set<FinancialEvaluation>();
    public DbSet<BidCostComponent> BidCostComponents => Set<BidCostComponent>();
    public DbSet<EvaluationReport> EvaluationReports => Set<EvaluationReport>();
    public DbSet<TenderApproval> TenderApprovals => Set<TenderApproval>();
    public DbSet<CommitteeAssignment> CommitteeAssignments => Set<CommitteeAssignment>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<CommitteeVote> CommitteeVotes => Set<CommitteeVote>();
    public DbSet<ComplianceReview> ComplianceReviews => Set<ComplianceReview>();
    public DbSet<ClarificationRequest> ClarificationRequests => Set<ClarificationRequest>();
    public DbSet<ClarificationResponse> ClarificationResponses => Set<ClarificationResponse>();
    public DbSet<ClarificationAttachment> ClarificationAttachments => Set<ClarificationAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AiBidAnalysis> AiBidAnalyses => Set<AiBidAnalysis>();
    public DbSet<TenderAward> TenderAwards => Set<TenderAward>();
    public DbSet<TenderPurchaseRequisition> TenderPurchaseRequisitions => Set<TenderPurchaseRequisition>();
    public DbSet<FaceCredential> FaceCredentials => Set<FaceCredential>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Add only the .NET 10 Identity passkey mapping; preserve existing V1 user/login/token columns.
        builder.Entity<IdentityUserPasskey<string>>(entity => {
            entity.ToTable("AspNetUserPasskeys");
            entity.HasKey(p => p.CredentialId);
            entity.Property(p => p.CredentialId).HasMaxLength(1024);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(p => p.UserId).IsRequired().OnDelete(DeleteBehavior.NoAction);
            entity.OwnsOne(p => p.Data).ToJson();
        });
        ProcurementModelConfiguration.Configure(builder);
        builder.Entity<FaceCredential>(entity => {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.ProtectedTemplate).HasMaxLength(8192).IsRequired();
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
