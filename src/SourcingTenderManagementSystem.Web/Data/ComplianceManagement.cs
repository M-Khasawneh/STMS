using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IComplianceManagement
{
    Task<CompliancePage> PageAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default);
    Task DecideAsync(ClaimsPrincipal user, Guid bidId, ComplianceDecisionForm form, CancellationToken ct = default);
    Task AskAsync(ClaimsPrincipal user, Guid bidId, ClarificationForm form, CancellationToken ct = default);
    Task ReplyAsync(ClaimsPrincipal user, Guid bidId, Guid requestId, ClarificationReplyForm form, CancellationToken ct = default);
    Task ResolveAsync(ClaimsPrincipal user, Guid bidId, Guid requestId, string version, CancellationToken ct = default);
    Task<BidDocumentDownload> DownloadAsync(ClaimsPrincipal user, Guid bidId, Guid attachmentId, CancellationToken ct = default);
}

// Uses the same tender transaction lock and fresh account/visibility queries as submitted bids.
public sealed class ComplianceManagement(ApplicationDbContext db, IBidData bids, TimeProvider clock,
    IWebHostEnvironment environment, ILogger<ComplianceManagement> logger) : IComplianceManagement
{
    private readonly IPrivateBidFiles files = PrivateBidFiles.ForClarifications(environment);

    private async Task<(BidAccount Account, BidSnapshot Bid, BidContext Context)> Access(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var account = await bids.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(account is not null && (account.Supplier || account.Internal), 403, "An authorized account is required.");
        var bid = await bids.ReadAsync(id, account, clock.GetUtcNow(), ct) ?? throw new BidException(404, "Offer not found.");
        var context = await bids.ContextAsync(bid.Bid.TenderId, ct) ?? throw new BidException(404, "Tender not found.");
        if (account.Supplier)
        {
            var participation = await bids.ParticipationAsync(bid.Bid.TenderId, account.SupplierId!.Value, ct);
            BidRules.Require(participation is not null && participation.Link.Id == bid.Bid.TenderSupplierId &&
                participation.Invitation.Status == InvitationStatus.Accepted, 403, "An accepted invitation is required.");
        }
        BidRules.Require(bid.Bid.SubmittedAt.HasValue && BidRules.ClosedStages.Contains(context.Tender.Status), 409,
            "Compliance information is available after tender closure.");
        return (account, bid, context);
    }

    public async Task<CompliancePage> PageAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default)
    {
        var access = await Access(user, bidId, ct);
        var reviews = await Reviews(bidId, ct);
        var requests = await Requests(bidId, ct);
        var threads = new List<ClarificationThread>();
        foreach (var request in requests)
        {
            var responses = await db.ClarificationResponses.AsNoTracking().Where(x => x.ClarificationRequestId == request.Id)
                .OrderBy(x => x.RespondedAt).ThenBy(x => x.Id).ToListAsync(ct);
            var replies = new List<ClarificationReply>();
            foreach (var response in responses)
                replies.Add(new(response, await db.ClarificationAttachments.AsNoTracking()
                    .Where(x => x.ClarificationResponseId == response.Id).OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).ToListAsync(ct)));
            threads.Add(new(request, replies));
        }
        await Access(user, bidId, ct);
        return new(bidId, access.Context.Tender.Title, ComplianceRules.Version(access.Bid.Bid, reviews, requests),
            ComplianceRules.Officer(access.Account) && access.Context.Tender.Status == TenderStatus.Closed,
            access.Account.Supplier && access.Context.Tender.Status == TenderStatus.Closed, access.Account.UserId, reviews, threads);
    }

    public async Task DecideAsync(ClaimsPrincipal user, Guid bidId, ComplianceDecisionForm form, CancellationToken ct = default)
    {
        ComplianceRules.Decision(form);
        await Change(user, bidId, form.Version, async access =>
        {
            ComplianceRules.Review(access.Account, access.Bid.Bid, access.Context.Tender);
            AddReview(access.Bid.Bid, access.Account.UserId, form.Outcome!.Value, form.Reasons);
            Audit(access.Account.UserId, bidId, "ComplianceReviewed", form.Outcome.Value.ToString(), form.Reasons);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task AskAsync(ClaimsPrincipal user, Guid bidId, ClarificationForm form, CancellationToken ct = default)
    {
        ComplianceRules.Text(form.Subject, 256, "Subject");
        ComplianceRules.Text(form.Description, 8000, "Question");
        await Change(user, bidId, form.Version, async access =>
        {
            ComplianceRules.Review(access.Account, access.Bid.Bid, access.Context.Tender);
            var recipient = await (from link in db.TenderSuppliers join supplier in db.SupplierProfiles on link.SupplierProfileId equals supplier.Id
                where link.Id == access.Bid.Bid.TenderSupplierId && link.TenderId == access.Bid.Bid.TenderId
                select supplier.UserId).SingleAsync(ct);
            db.ClarificationRequests.Add(new ClarificationRequest {
                Id = Guid.NewGuid(), BidId = bidId, SenderUserId = access.Account.UserId, RecipientUserId = recipient,
                Subject = form.Subject.Trim(), Description = form.Description.Trim(), Status = ClarificationStatus.Pending, CreatedAt = clock.GetUtcNow()
            });
            AddReview(access.Bid.Bid, access.Account.UserId, ComplianceStatus.PendingClarification, form.Description.Trim());
            Notify(recipient, access.Bid.Bid, "Clarification requested", "A Sourcing Officer has requested clarification on your offer.");
            Audit(access.Account.UserId, bidId, "ClarificationRequested", "Pending", null);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task ReplyAsync(ClaimsPrincipal user, Guid bidId, Guid requestId, ClarificationReplyForm form, CancellationToken ct = default)
    {
        ComplianceRules.Text(form.Response, 8000, "Response");
        ComplianceRules.AttachmentCount(form.Files.Count);
        Guid? tenderId = null;
        try
        {
            await Change(user, bidId, form.Version, async access =>
            {
                tenderId = access.Context.Tender.Id;
                var request = await db.ClarificationRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.BidId == bidId, ct)
                    ?? throw new BidException(404, "Clarification not found.");
                ComplianceRules.Reply(access.Account, request, access.Context.Tender);
                await RecoverLocked(bidId, tenderId.Value, ct);
                var response = new ClarificationResponse {
                    Id = Guid.NewGuid(), ClarificationRequestId = request.Id, ResponderUserId = access.Account.UserId,
                    Response = form.Response.Trim(), RespondedAt = clock.GetUtcNow()
                };
                db.ClarificationResponses.Add(response);
                foreach (var upload in form.Files)
                {
                    var validated = await SupplierUploadValidation.ReadAsync(upload, ct);
                    var reference = PrivateBidFiles.NewReference(bidId, validated.Bytes);
                    var intent = new BidFileIntent(Guid.NewGuid(), bidId, tenderId.Value, reference, null);
                    await files.WriteIntentAsync(intent, ct);
                    await files.SaveAsync(bidId, reference, validated, ct);
                    db.ClarificationAttachments.Add(new ClarificationAttachment {
                        Id = Guid.NewGuid(), ClarificationResponseId = response.Id, OriginalName = validated.OriginalName,
                        ContentType = validated.ContentType, Size = validated.Bytes.LongLength, StorageReference = reference,
                        Sha256 = Convert.ToHexString(SHA256.HashData(validated.Bytes)), UploadedAt = clock.GetUtcNow()
                    });
                }
                request.Status = ClarificationStatus.Answered;
                Notify(request.SenderUserId, access.Bid.Bid, "Clarification answered", "The supplier has replied to your clarification request.");
                Audit(access.Account.UserId, bidId, "ClarificationAnswered", "Answered", null);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (InvalidDataException) { throw new BidException(400, "Use matching PDF, JPG or PNG documents, at most 5 MB each."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new BidException(503, "Private attachment storage is unavailable. Reload the page before retrying."); }
        finally
        {
            if (tenderId.HasValue)
            {
                try
                {
                    await using var tx = await bids.BeginAsync(tenderId.Value, CancellationToken.None);
                    await RecoverLocked(bidId, tenderId.Value, CancellationToken.None);
                    await tx.CommitAsync(CancellationToken.None);
                }
                catch (Exception e) { logger.LogWarning(e, "Clarification attachment recovery pending for bid {BidId}.", bidId); }
            }
        }
    }

    public async Task ResolveAsync(ClaimsPrincipal user, Guid bidId, Guid requestId, string version, CancellationToken ct = default)
    {
        await Change(user, bidId, version, async access =>
        {
            ComplianceRules.Review(access.Account, access.Bid.Bid, access.Context.Tender);
            var request = await db.ClarificationRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.BidId == bidId, ct)
                ?? throw new BidException(404, "Clarification not found.");
            BidRules.Require(request.SenderUserId == access.Account.UserId, 403, "Only the requesting officer may resolve this clarification.");
            BidRules.Require(request.Status == ClarificationStatus.Answered, 409, "Only an answered clarification can be resolved.");
            request.Status = ClarificationStatus.Resolved;
            Audit(access.Account.UserId, bidId, "ClarificationResolved", "Resolved", null);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task<BidDocumentDownload> DownloadAsync(ClaimsPrincipal user, Guid bidId, Guid attachmentId, CancellationToken ct = default)
    {
        Stream? stream = null;
        try
        {
            var access = await Access(user, bidId, ct);
            await using var tx = await bids.BeginAsync(access.Context.Tender.Id, ct);
            await Access(user, bidId, ct);
            await RecoverLocked(bidId, access.Context.Tender.Id, ct);
            var attachment = await (from a in db.ClarificationAttachments.AsNoTracking()
                join r in db.ClarificationResponses on a.ClarificationResponseId equals r.Id
                join q in db.ClarificationRequests on r.ClarificationRequestId equals q.Id
                where a.Id == attachmentId && q.BidId == bidId select a).SingleOrDefaultAsync(ct)
                ?? throw new BidException(404, "Attachment not found.");
            stream = await files.OpenVerifiedAsync(new BidDocument {
                Id = attachment.Id, BidId = bidId, StorageReference = attachment.StorageReference,
                OriginalName = attachment.OriginalName, ContentType = attachment.ContentType, Size = attachment.Size
            }, ct);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            BidRules.Require(hash == attachment.Sha256, 409, "Attachment verification failed.");
            stream.Position = 0;
            await Access(user, bidId, ct);
            await tx.CommitAsync(ct);
            var result = new BidDocumentDownload(stream, attachment.OriginalName, "application/octet-stream", attachment.Size);
            stream = null;
            return result;
        }
        catch (InvalidDataException) { throw new BidException(409, "Attachment verification failed."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new BidException(503, "The private attachment is temporarily unavailable."); }
        finally { if (stream is not null) await stream.DisposeAsync(); }
    }

    private async Task Change(ClaimsPrincipal user, Guid bidId, string version,
        Func<(BidAccount Account, BidSnapshot Bid, BidContext Context), Task> action, CancellationToken ct)
    {
        await BidManagement.Mutate(async () =>
        {
            var preliminary = await Access(user, bidId, ct);
            await using var tx = await bids.BeginAsync(preliminary.Context.Tender.Id, ct);
            var access = await Access(user, bidId, ct);
            ComplianceRules.Version(version, ComplianceRules.Version(access.Bid.Bid, await Reviews(bidId, ct), await Requests(bidId, ct)));
            await action(access);
            var final = await Access(user, bidId, ct);
            if (access.Account.Supplier) BidRules.Require(final.Account.Supplier, 403, "Supplier eligibility changed.");
            else ComplianceRules.Review(final.Account, final.Bid.Bid, final.Context.Tender);
            await tx.CommitAsync(ct);
            return true;
        });
    }
    private Task<List<ComplianceReview>> Reviews(Guid id, CancellationToken ct) => db.ComplianceReviews.AsNoTracking()
        .Where(x => x.BidId == id).OrderByDescending(x => x.ReviewedAt).ThenBy(x => x.Id).ToListAsync(ct);
    private Task<List<ClarificationRequest>> Requests(Guid id, CancellationToken ct) => db.ClarificationRequests.AsNoTracking()
        .Where(x => x.BidId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

    private void AddReview(Bid bid, string actor, ComplianceStatus outcome, string? reasons)
    {
        db.ComplianceReviews.Add(new ComplianceReview { Id = Guid.NewGuid(), BidId = bid.Id, ReviewerUserId = actor,
            Outcome = outcome, Reasons = reasons?.Trim(), ReviewedAt = clock.GetUtcNow() });
        // Attach only the existing row and mark only Status. Commercial contents and original documents remain immutable.
        db.Attach(bid);
        bid.Status = outcome switch { ComplianceStatus.Compliant => BidStatus.Compliant,
            ComplianceStatus.NonCompliant => BidStatus.NonCompliant, _ => BidStatus.UnderReview };
        db.Entry(bid).Property(x => x.Status).IsModified = true;
    }
    private void Audit(string actor, Guid bidId, string action, string status, string? reason) => db.AuditLogs.Add(new AuditLog {
        Id = Guid.NewGuid(), ActorUserId = actor, RecordType = "Bid", RecordId = bidId.ToString(), Action = action,
        NewStatus = status, Reason = reason?.Trim(), OccurredAt = clock.GetUtcNow()
    });
    private void Notify(string recipient, Bid bid, string subject, string message) => db.Notifications.Add(new Notification {
        Id = Guid.NewGuid(), RecipientUserId = recipient, BidId = bid.Id, TenderId = bid.TenderId,
        Subject = subject, Message = message, CreatedAt = clock.GetUtcNow()
    });
    private async Task RecoverLocked(Guid bidId, Guid tenderId, CancellationToken ct)
    {
        foreach (var intent in await files.IntentsAsync(bidId, ct))
        {
            BidRules.Require(intent.TenderId == tenderId && intent.OldReference is null, 409, "Attachment journal requires review.");
            if (!await db.ClarificationAttachments.AsNoTracking().AnyAsync(x => x.StorageReference == intent.NewReference, ct))
                await files.DeleteAsync(bidId, intent.NewReference!, ct);
            await files.CompleteIntentAsync(intent, ct);
        }
    }
}
