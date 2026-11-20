using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Models.TopCommittee;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static partial class IsolatedSqlAcceptance
{
    private static async Task Journey(IServiceProvider provider, TenderRuleChecks.TestClock clock, FailureProbe failure)
    {
        async Task<T> Call<S, T>(Func<S, Task<T>> work) where S : notnull { using var scope = provider.CreateScope(); return await work(scope.ServiceProvider.GetRequiredService<S>()); }
        async Task Run<S>(Func<S, Task> work) where S : notnull { using var scope = provider.CreateScope(); await work(scope.ServiceProvider.GetRequiredService<S>()); }
        async Task Deny(Func<Task> work, int status) { try { await work(); throw new Exception("Expected rejection"); } catch (BidException e) { Check(e.Status == status, "Bid/decision access or state guard " + status); } catch (TenderException e) { Check(e.Status == status, "Tender guard " + status); } catch (RequisitionException e) { Check(e.Status == status, "Requisition guard " + status); } }
        var requestDepartment = Guid.NewGuid(); var sourcingDepartment = Guid.NewGuid(); var identities = new Dictionary<string, string>();
        ClaimsPrincipal Actor(string key) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, identities[key])], "IsolatedSqlAcceptance"));
        var password = "Aa1!" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            db.Departments.AddRange(new Department { Id = requestDepartment, Name = "Acceptance requesting" }, new Department { Id = sourcingDepartment, Name = "Acceptance sourcing" }); await db.SaveChangesAsync();
            foreach (var (name, role, department) in new[] {
                ("owner",ApplicationRoles.RequestingDepartment,requestDepartment), ("colleague",ApplicationRoles.RequestingDepartment,requestDepartment),
                ("officer",ApplicationRoles.SourcingOfficer,sourcingDepartment), ("manager",ApplicationRoles.SourcingManager,sourcingDepartment),
                ("department-member",ApplicationRoles.EvaluationCommittee,requestDepartment), ("financial",ApplicationRoles.EvaluationCommittee,sourcingDepartment), ("technical",ApplicationRoles.EvaluationCommittee,sourcingDepartment),
                ("top0",ApplicationRoles.TopCommittee,sourcingDepartment), ("top1",ApplicationRoles.TopCommittee,sourcingDepartment), ("top2",ApplicationRoles.TopCommittee,sourcingDepartment), ("top3",ApplicationRoles.TopCommittee,sourcingDepartment), ("admin",ApplicationRoles.Admin,sourcingDepartment) })
            {
                var user = new IdentityUser("acceptance-" + name); Check((await users.CreateAsync(user, password)).Succeeded && (await users.AddToRoleAsync(user, role)).Succeeded, "Isolated Identity account/role: " + name);
                identities[name] = user.Id; db.EmployeeDepartments.Add(new EmployeeDepartment { UserId = user.Id, DepartmentId = department });
            }
            await db.SaveChangesAsync();
        }
        var pdf = DocumentExtractionChecks.TextPdf();
        IFormFile File() => new FormFile(new MemoryStream(pdf), 0, pdf.Length, "document", "synthetic.pdf");
        var supplierIds = new List<Guid>();
        for (int i = 0; i < 2; i++)
        {
            var key = "supplier" + i;
            var registered = await Call<SupplierRegistration, IdentityResult>(s => s.RegisterAsync(new SupplierRegistrationViewModel
            {
                CompanyName = "Synthetic acceptance company " + i,
                RegistrationNumber = "ACCEPT-R-" + i,
                TaxNumber = "ACCEPT-T-" + i,
                ContactName = "Synthetic contact",
                ContactEmail = "supplier@example.invalid",
                ContactPhone = "+96265550100",
                UserName = key,
                Password = password,
                ConfirmPassword = password,
                CompanyCertificate = File(),
                TradeLicense = File(),
                TaxCertificate = File()
            }, default));
            Check(registered.Succeeded, "Supplier registration persists company, Identity password and three private documents");
            var profile = await Call<ApplicationDbContext, SupplierProfile>(db => db.SupplierProfiles.AsNoTracking().SingleAsync(s => db.Users.Any(u => u.Id == s.UserId && u.UserName == key)));
            identities[key] = profile.UserId; supplierIds.Add(profile.Id); Check(profile.Status == SupplierStatus.Pending, "New registration remains Pending");
            Check(await Call<SupplierReview, string?>(s => s.DecideAsync(profile.Id, SupplierStatus.Pending, SupplierStatus.Approved, null, identities["admin"], default)) is null, "Admin approval through existing supplier service");
        }
        var requestId = Guid.Empty; var tenderId = Guid.Empty; var bidId = Guid.Empty;
        async Task<RequisitionDetails> Request(string who) => await Call<RequisitionManagement, RequisitionDetails>(async s => await s.DetailsAsync(requestId, await s.ActorAsync(Actor(who))));
        Task<TenderDetails> Tender(string who = "officer") => Call<TenderManagement, TenderDetails>(async s => await s.DetailsAsync(tenderId, await s.ActorAsync(Actor(who))));
        requestId = await Call<RequisitionManagement, Guid>(async s => await s.SaveAsync(null, await s.ActorAsync(Actor("owner")), new RequisitionForm { Title = "Synthetic linked acceptance request", Reason = "Synthetic integrated test", Priority = "High", TargetDate = DateOnly.FromDateTime(clock.Utc.AddDays(7).Date), Items = [new() { Description = "Synthetic equipment", Specifications = "Acceptance-only specification", Quantity = 2.5m }] }, false));
        Check((await Request("owner")).Request.Status == PurchaseRequestStatus.Draft, "Purchase request saved as Draft");
        await Deny(async () => { await Request("colleague"); }, 404);
        var version = (await Request("owner")).Version;
        await Run<RequisitionManagement>(async s => await s.SubmitAsync(requestId, await s.ActorAsync(Actor("owner")), version));
        foreach (var status in new[] { PurchaseRequestStatus.UnderReview, PurchaseRequestStatus.Approved })
        { var page = await Request("officer"); await Run<RequisitionManagement>(async s => await s.ReviewAsync(requestId, await s.ActorAsync(Actor("officer")), new() { Version = page.Version, Decision = status, Comments = "Synthetic approved review" })); }
        Check((await Request("owner")).Request.Status == PurchaseRequestStatus.Approved, "Explicit officer review and approval persisted");
        var tenderForm = new TenderForm { RequisitionId = requestId, Title = "Synthetic connected SQL tender", Description = "Isolated test only", Terms = "Synthetic terms", Start = TenderTime.Local(clock.Utc.AddMinutes(-1)), Deadline = TenderTime.Local(clock.Utc.AddHours(1)) };
        tenderId = await Call<TenderManagement, Guid>(async s => await s.SaveAsync(null, await s.ActorAsync(Actor("officer")), tenderForm));
        Check((await Request("owner")).Request.Status == PurchaseRequestStatus.ConvertedToTender && (await Tender()).Source.Id == requestId, "Draft conversion updates and links exactly the approved source request");
        await Deny(async () => { await Call<TenderManagement, Guid>(async s => await s.SaveAsync(null, await s.ActorAsync(Actor("officer")), tenderForm)); }, 409);
        var tender = await Tender();
        await Run<TenderManagement>(async s => await s.SaveCriteriaAsync(tenderId, await s.ActorAsync(Actor("officer")), new() { Version = tender.Version, Items = [new() { Name = "Synthetic technical", Description = "Test input, not a default", Kind = EvaluationKind.Technical, Weight = 60 }, new() { Name = "Synthetic commercial", Description = "Test input, not a default", Kind = EvaluationKind.Financial, Weight = 40 }] }));
        tender = await Tender(); await Run<TenderManagement>(async s => await s.ApproveCriteriaAsync(tenderId, await s.ActorAsync(Actor("manager")), tender.Version));
        tender = await Tender(); await Run<TenderManagement>(async s => await s.ChangeStatusAsync(tenderId, await s.ActorAsync(Actor("officer")), tender.Version, TenderStatus.Published));
        tender = await Tender(); await Run<TenderManagement>(async s => await s.InviteAsync(tenderId, await s.ActorAsync(Actor("officer")), new() { Version = tender.Version, SupplierIds = supplierIds }));
        var inbox = await Call<TenderManagement, IReadOnlyList<InvitationRow>>(async s => await s.InboxAsync(await s.ActorAsync(Actor("supplier0"))));
        var invitation = inbox.Single(); await Run<TenderManagement>(async s => await s.RespondAsync(invitation.Invitation.Id, await s.ActorAsync(Actor("supplier0")), TenderManagement.InvitationVersion(invitation), InvitationStatus.Accepted));
        tender = await Tender(); await Run<TenderManagement>(async s => await s.ChangeStatusAsync(tenderId, await s.ActorAsync(Actor("officer")), tender.Version, TenderStatus.Open));
        Check((await Tender()).Tender.Status == TenderStatus.Open, "Configured/approved criteria, publication, invitation and opening complete");
        var item = (await Tender()).Items.Single();
        bidId = await Call<BidManagement, Guid>(s => s.CreateDraftAsync(Actor("supplier0"), tenderId, new() { DeliveryTimeframe = "14 days", TechnicalDetails = "Synthetic specification response", PaymentTerms = "30 days", OfferValidUntil = DateOnly.FromDateTime(clock.Utc.AddDays(7).Date), Items = [new() { ItemId = item.Id, UnitPrice = 100 }], Charges = [new() { CostType = "Synthetic fee", Amount = 5 }] }));
        // Exercise upgrade and duplicate-data refusal only in this owned, synthetic database.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261116032101_CameraFaceSettings");
            var originalBid = await db.Bids.AsNoTracking().SingleAsync(b => b.Id == bidId);
            var duplicate = new Bid { Id = Guid.NewGuid(), TenderSupplierId = originalBid.TenderSupplierId, TenderId = originalBid.TenderId, Status = BidStatus.Draft, Currency = "JOD", DeliveryTimeframe = "Synthetic duplicate probe", PaymentTerms = "Synthetic" };
            db.Bids.Add(duplicate); await db.SaveChangesAsync();
            try { await migrator.MigrateAsync(); throw new Exception("Duplicate migration unexpectedly succeeded"); }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 51000)
            { Check(await db.Bids.CountAsync(b => b.TenderSupplierId == originalBid.TenderSupplierId) == 2 && !(await db.Database.GetAppliedMigrationsAsync()).Contains("20261116152333_BidParticipationUniqueness"), "Duplicate-data migration refuses safely without changing bids or history"); }
            db.Bids.Remove(duplicate); await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            Check((await db.Database.GetAppliedMigrationsAsync()).SequenceEqual(db.Database.GetMigrations()), "Clean isolated upgrade restores all migrations in order");
            db.Bids.Add(duplicate);
            try { await db.SaveChangesAsync(); throw new Exception("Unique index accepted duplicate bid"); }
            catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            { db.ChangeTracker.Clear(); Check(await db.Bids.CountAsync(b => b.TenderSupplierId == originalBid.TenderSupplierId) == 1, "SQL unique participation index rejects duplicate bid and retains original"); }
        }
        Task<BidDetails> Bid() => Call<BidManagement, BidDetails>(s => s.DetailsAsync(Actor("supplier0"), bidId));
        var draft = await Bid();
        await Run<BidManagement>(s => s.SaveDraftAsync(Actor("supplier0"), bidId, new() { Version = draft.Version, DeliveryTimeframe = "14 days", TechnicalDetails = "Corrected synthetic specification response", PaymentTerms = "30 days", OfferValidUntil = DateOnly.FromDateTime(clock.Utc.AddDays(7).Date), Items = [new() { ItemId = item.Id, UnitPrice = 100 }], Charges = [new() { CostType = "Synthetic fee", Amount = 5 }] }));
        Check((await Bid()).Snapshot.Bid.TechnicalDetails == "Corrected synthetic specification response", "Eligible draft edit persists before explicit submission");
        Check((await Bid()).Total == 255m && (await Bid()).Snapshot.Items.Single().OfferedQuantity == 2.5m, "Authoritative fractional quantity and decimal totals persist");
        await Deny(async () => { await Call<BidManagement, BidDetails>(s => s.DetailsAsync(Actor("supplier1"), bidId)); }, 404);
        var bid = await Bid(); await Run<BidDocumentManagement>(async s => { await s.UploadAsync(Actor("supplier0"), bidId, bid.Version, "Technical Proposal", File()); });
        bid = await Bid(); var documentId = bid.Snapshot.Documents.Single().Id;
        await Deny(async () => { await using var denied = await Call<BidDocumentManagement, BidDocumentDownload>(s => s.DownloadAsync(Actor("supplier1"), bidId, documentId)); }, 404);
        failure.Action = "BidSubmitted";
        try { await Run<BidManagement>(s => s.SubmitAsync(Actor("supplier0"), bidId, bid.Version)); throw new Exception("Expected SQL rollback"); } catch (InjectedFailure) { Check((await Bid()).Snapshot.Bid.Status == BidStatus.Draft && await Call<ApplicationDbContext, int>(db => db.AuditLogs.CountAsync(a => a.Action == "BidSubmitted")) == 0 && await Call<ApplicationDbContext, int>(db => db.Notifications.CountAsync(n => n.BidId == bidId)) == 0, "Post-SaveChanges submission failure rolls back bid, audit and receipt in SQL"); } finally { failure.Action = null; }
        bid = await Bid(); await Run<BidManagement>(s => s.SubmitAsync(Actor("supplier0"), bidId, bid.Version));
        Check((await Bid()).Snapshot.Bid.SubmittedAt.HasValue, "Explicit final bid submission persists UTC timestamp and real document verification");
        await Deny(() => Run<BidDocumentManagement>(s => s.DeleteAsync(Actor("supplier0"), bidId, documentId, bid.Version)), 409);
        clock.Utc = clock.Utc.AddHours(2); tender = await Tender();
        await Run<TenderManagement>(async s => await s.ChangeStatusAsync(tenderId, await s.ActorAsync(Actor("officer")), tender.Version, TenderStatus.Closed));
        Check(await Call<ApplicationDbContext, bool>(db => db.Invitations.AnyAsync(i => i.Status == InvitationStatus.Expired)), "Unanswered invitation expires on explicit close after deadline");
        var compliance = await Call<IComplianceManagement, CompliancePage>(s => s.PageAsync(Actor("officer"), bidId));
        await Run<IComplianceManagement>(s => s.DecideAsync(Actor("officer"), bidId, new() { Version = compliance.Version, Outcome = ComplianceStatus.Compliant, Reasons = "Synthetic compliance completed" }));
        var committee = await Call<CommitteeManagement, CommitteePage>(s => s.PageAsync(Actor("manager"), tenderId));
        await Run<CommitteeManagement>(s => s.SaveAsync(Actor("manager"), tenderId, new() { Version = committee.Form.Version, ManagerUserId = identities["manager"], DepartmentRepresentativeId = identities["department-member"], FinancialRepresentativeId = identities["financial"], TechnicalRepresentativeId = identities["technical"] }));
        tender = await Tender(); await Run<IEvaluationManagement>(s => s.StartAsync(Actor("officer"), tenderId, tender.Version));
        foreach (var evaluator in new[] { "manager", "department-member", "financial", "technical" })
        {
            var evaluation = await Call<IEvaluationManagement, EvaluationPage>(s => s.PageAsync(Actor(evaluator), bidId));
            await Run<IEvaluationManagement>(s => s.SaveAsync(Actor(evaluator), bidId, new() { Version = evaluation.Form.Version, Items = evaluation.Criteria.Select(c => new EvaluationLine { CriterionId = c.Id, Score = 90, Notes = "Synthetic acceptance observation" }).ToList() }, true));
        }
        var evaluated = await Call<IEvaluationManagement, EvaluationPage>(s => s.PageAsync(Actor("technical"), bidId)); Check(evaluated.ConfirmedMembers == 4 && evaluated.Results.Count == 2, "All four appointed members confirm both criteria in SQL");
        var report = await Call<IUnifiedReportManagement, UnifiedReportPage>(s => s.PageAsync(Actor("technical"), tenderId));
        var reportForm = new UnifiedReportForm { Version = report.Form.Version, RecommendedBidId = bidId, Observations = "Synthetic complete comparison", FinalResult = "Four confirmed evaluations", Recommendation = "Synthetic recommendation for the compliant offer" };
        await Run<IUnifiedReportManagement>(s => s.SaveAsync(Actor("technical"), tenderId, reportForm, false));
        report = await Call<IUnifiedReportManagement, UnifiedReportPage>(s => s.PageAsync(Actor("technical"), tenderId)); reportForm.Version = report.Form.Version;
        failure.Action = "ReportSubmitted";
        try { await Run<IUnifiedReportManagement>(s => s.SaveAsync(Actor("technical"), tenderId, reportForm, true)); throw new Exception("Expected SQL report rollback"); } catch (InjectedFailure) { Check((await Tender()).Tender.Status == TenderStatus.UnderEvaluation && await Call<ApplicationDbContext, int>(db => db.AuditLogs.CountAsync(a => a.Action == "ReportSubmitted")) == 0 && await Call<ApplicationDbContext, int>(db => db.Notifications.CountAsync(n => n.Subject == "Unified report submitted")) == 0, "Post-save report failure rolls tender, audit and notification transaction back"); } finally { failure.Action = null; }
        report = await Call<IUnifiedReportManagement, UnifiedReportPage>(s => s.PageAsync(Actor("technical"), tenderId)); reportForm.Version = report.Form.Version;
        await Run<IUnifiedReportManagement>(s => s.SaveAsync(Actor("technical"), tenderId, reportForm, true));
        report = await Call<IUnifiedReportManagement, UnifiedReportPage>(s => s.PageAsync(Actor("manager"), tenderId));
        await Run<IUnifiedReportManagement>(s => s.DecideAsync(Actor("manager"), tenderId, new() { Version = report.DecisionForm.Version, Decision = DecisionStatus.ManagerApproved, Accept = true, Comments = "Synthetic manager approval" }));
        var top = await Call<ITopCommitteeManagement, TopPage>(s => s.PageAsync(Actor("manager"), tenderId));
        await Run<ITopCommitteeManagement>(s => s.AssignAsync(Actor("manager"), tenderId, new() { Version = top.Version, Users = Enumerable.Range(0, 4).Select(i => identities["top" + i]).ToArray() }));
        top = await Call<ITopCommitteeManagement, TopPage>(s => s.PageAsync(Actor("manager"), tenderId)); await Run<ITopCommitteeManagement>(s => s.OpenAsync(Actor("manager"), tenderId, new() { Version = top.Version, Confirm = true }));
        for (int i = 0; i < 4; i++)
        {
            var who = "top" + i; var page = await Call<ITopCommitteeManagement, TopPage>(s => s.PageAsync(Actor(who), tenderId));
            var vote = new TopActionForm { Version = page.Version, RoundId = page.OpenRoundId, Choice = DecisionStatus.CommitteeApproved, Confirm = true, Reason = "Synthetic individual review and approval" };
            if (i == 0)
            {
                async Task<int> Vote() { try { await Run<ITopCommitteeManagement>(s => s.VoteAsync(Actor(who), tenderId, vote)); return 200; } catch (BidException e) { return e.Status; } }
                var results = await Task.WhenAll(Vote(), Vote()); Check(results.Count(x => x == 200) == 1 && results.Count(x => x == 409) == 1, "Concurrent same-member SQL votes persist once");
                var hidden = await Call<ITopCommitteeManagement, TopPage>(s => s.PageAsync(Actor("officer"), tenderId)); Check(hidden.Rounds.Single().Votes.Count == 0, "Partial votes stay sealed from the officer");
            }
            else await Run<ITopCommitteeManagement>(s => s.VoteAsync(Actor(who), tenderId, vote));
        }
        top = await Call<ITopCommitteeManagement, TopPage>(s => s.PageAsync(Actor("officer"), tenderId)); Check(top.CanAward && top.Rounds.Single().Complete, "Four actual SQL votes produce a collective approved round");
        var awardForm = new TopActionForm { Version = top.Version, RoundId = top.Rounds.Single().Round.Id, Confirm = true };
        async Task<int> Award() { try { await Run<ITopCommitteeManagement>(s => s.AwardAsync(Actor("officer"), tenderId, awardForm)); return 200; } catch (BidException e) { return e.Status; } }
        var awarded = await Task.WhenAll(Award(), Award()); Check(awarded.Count(x => x == 200) == 1 && awarded.Count(x => x == 409) == 1, "Concurrent SQL award commits exactly once");
        var final = await Call<ITopCommitteeManagement, TopPage>(s => s.PageAsync(Actor("officer"), tenderId)); Check(final.Tender.Status == TenderStatus.Awarded && final.Award?.BidId == bidId, "Final persisted award links the winning offer and tender");
        var outcome = await Call<ITopCommitteeManagement, TenderOutcome>(s => s.OutcomeAsync(Actor("supplier0"), tenderId)); Check(outcome.Result == "Your submitted offer was selected.", "Supplier sees only their own winning outcome");
        await Deny(async () => { await Call<IReporting, ReportPage>(s => s.ReadAsync(Actor("admin"), new() { Category = "awards" }, default)); }, 403);
        var awardReport = await Call<IReporting, ReportPage>(s => s.ReadAsync(Actor("officer"), new() { Category = "awards", Search = "Synthetic acceptance company 0" }, default));
        Check(awardReport.Document.Tables.Single().Rows.Count == 1 && awardReport.Document.Tables.Single().Rows.Single()[3].Text == "Not recorded", "Positive SQL winner search returns the actual award without fabricating a contract reference");
        var topReport = await Call<IReporting, ReportPage>(s => s.ReadAsync(Actor("top0"), new() { Category = "awards", TenderId = tenderId }, default));
        Check(topReport.Document.Tables.Single().Rows.Single()[6].Text == bidId.ToString(), "Actual assigned Top Committee SQL account reads the authorized winning award");
        var awardedDay = DateOnly.FromDateTime(TenderTime.Local(clock.Utc));
        var dated = await Call<IReporting, ReportPage>(s => s.ReadAsync(Actor("officer"), new() { Category = "awards", From = awardedDay, To = awardedDay, DepartmentId = requestDepartment }, default));
        Check(dated.Document.Tables.Single().Rows.Count == 1, "Positive SQL award-date/department filters retain the source row");
        Check(ReportExports.Excel(awardReport.Document).Length > 100 && ReportExports.Pdf(awardReport.Document).Length > 100, "Connected award data exports as genuine Excel and PDF");
        var activity = await Call<IReporting, ReportPage>(s => s.ReadAsync(Actor("officer"), new() { Category = "tenders" }, default));
        Check(activity.Document.Tables.First().Rows.Single()[5].Number == 50m, "Real connected tender participation is one submission / two invitations = 50%");
        Check(await Call<ApplicationDbContext, bool>(db => db.Notifications.AnyAsync()) && await Call<ApplicationDbContext, bool>(db => db.AuditLogs.AnyAsync(a => a.Action == "AwardRecorded")), "Workflow notifications and final award audit persist");
    }
}
