using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed record BidDocumentDownload(Stream Content, string OriginalName, string ContentType, long Size) : IAsyncDisposable
{
    // Phase 3 must apply these headers and serve as an attachment, never as an inline executable resource.
    public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string> {
        ["Cache-Control"] = "no-store", ["X-Content-Type-Options"] = "nosniff", ["Content-Security-Policy"] = "sandbox; default-src 'none'" };
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed class BidSubmissionDocuments(IPrivateBidFiles files) : IBidSubmissionDocuments
{
    public async Task VerifyAsync(BidSnapshot snapshot, CancellationToken ct)
    {
        BidRules.Require(snapshot.Documents.Count is > 0 and <= 10 && snapshot.Documents.Any(x => x.DocumentType == "Technical Proposal"), 400, "A Technical Proposal and at most ten documents are required.");
        foreach (var document in snapshot.Documents)
        {
            BidRules.Require(document.BidId == snapshot.Bid.Id && BidDocumentManagement.ValidType(document.DocumentType), 400, "Invalid bid document ownership or type.");
            try { await using var verified = await files.OpenVerifiedAsync(document, ct); }
            catch (InvalidDataException) { throw new BidException(409, "A bid document failed content verification. Replace the document before submitting."); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new BidException(503, "A private bid document is unavailable. No bid was submitted."); }
        }
    }
}

// No HTTP actions or public file paths: Phase 3 supplies MVC transport around these protected operations.
public sealed class BidDocumentManagement(IBidDocumentData data, BidManagement bids, IPrivateBidFiles files,
    TimeProvider clock, ILogger<BidDocumentManagement> logger)
{
    public static bool ValidType(string type) => type is "Technical Proposal" or "Financial Proposal" or "Supporting Document";
    public Task<Guid> UploadAsync(ClaimsPrincipal user, Guid bidId, string version, string type, IFormFile file, CancellationToken ct = default) =>
        ChangeAsync(user, bidId, null, version, type, file, false, ct);
    public Task<Guid> ReplaceAsync(ClaimsPrincipal user, Guid bidId, Guid documentId, string version, string type, IFormFile file, CancellationToken ct = default) =>
        ChangeAsync(user, bidId, documentId, version, type, file, false, ct);
    public async Task DeleteAsync(ClaimsPrincipal user, Guid bidId, Guid documentId, string version, CancellationToken ct = default) =>
        _ = await ChangeAsync(user, bidId, documentId, version, null, null, true, ct);

    private async Task<Guid> ChangeAsync(ClaimsPrincipal user, Guid bidId, Guid? documentId, string version,
        string? type, IFormFile? file, bool delete, CancellationToken ct)
    {
        BidFileIntent? intent = null;
        try
        {
            return await BidManagement.Mutate(async () =>
            {
                var preliminary = await bids.Read(user, bidId, ct);
                await using var tx = await data.BeginAsync(preliminary.Bid.TenderId, ct);
                var snapshot = await bids.Read(user, bidId, ct);
                var context = await bids.Context(snapshot.Bid.TenderId, ct);
                await bids.OwnDraft(user, snapshot, context, ct);
                BidManagement.Version(snapshot, version);
                await RecoverLockedAsync(bidId, context.Tender.Id, ct);
                var old = documentId.HasValue ? snapshot.Documents.SingleOrDefault(x => x.Id == documentId) : null;
                BidRules.Require(!documentId.HasValue || old is not null, 404, "Document not found in this bid.");
                BidRules.Require(delete || documentId.HasValue || snapshot.Documents.Count < 10, 400, "A bid may have at most ten attachments.");
                var documents = snapshot.Documents.Where(x => x.Id != documentId).ToList();
                ValidatedSupplierFile? upload = null;
                if (!delete)
                {
                    BidRules.Require(type is not null && ValidType(type), 400, "Choose Technical Proposal, Financial Proposal or Supporting Document.");
                    upload = await SupplierUploadValidation.ReadAsync(file, ct);
                }
                var reference = upload is null ? null : PrivateBidFiles.NewReference(bidId, upload.Bytes);
                intent = new(Guid.NewGuid(), bidId, context.Tender.Id, reference, old?.StorageReference);
                // Durable intent is written before any new bytes or metadata can be created.
                await files.WriteIntentAsync(intent, ct);
                if (upload is not null)
                {
                    await files.SaveAsync(bidId, reference!, upload, ct);
                    documents.Add(new BidDocument { Id = old?.Id ?? Guid.NewGuid(), BidId = bidId, DocumentType = type!,
                        OriginalName = upload.OriginalName, ContentType = upload.ContentType, Size = upload.Bytes.LongLength,
                        StorageReference = reference!, UploadedAt = clock.GetUtcNow().ToUniversalTime() });
                }
                await data.SaveDocumentsAsync(bidId, documents, ct);
                // Tender lock is retained across file IO and persistence. Recheck account and time at commit.
                await bids.OwnDraft(user, snapshot, context, ct);
                await tx.CommitAsync(ct);
                return delete ? documentId!.Value : documents.Single(x => x.StorageReference == reference).Id;
            });
        }
        catch (InvalidDataException) { throw new BidException(400, "Invalid bid file. Use a matching PDF, JPG or PNG file up to 5 MB."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new BidException(503, "Private bid file storage is unavailable. Reload the bid before retrying."); }
        finally
        {
            // This runs after the mutation transaction has been disposed. Even an uncertain Commit result is
            // resolved from committed references; never delete a file simply because Commit threw.
            if (intent is not null) await RecoverAfterOperationAsync(intent);
        }
    }
    private async Task RecoverAfterOperationAsync(BidFileIntent intent)
    {
        try
        {
            await using var tx = await data.BeginAsync(intent.TenderId, CancellationToken.None);
            await RecoverLockedAsync(intent.BidId, intent.TenderId, CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            // Leave durable intents in place for the next authorized operation or explicit recovery retry.
            logger.LogWarning(e, "Private bid cleanup pending for operation {OperationId}, bid {BidId}.", intent.Id, intent.BidId);
        }
    }
    private async Task RecoverLockedAsync(Guid bidId, Guid tenderId, CancellationToken ct)
    {
        foreach (var intent in await files.IntentsAsync(bidId, ct))
        {
            BidRules.Require(intent.TenderId == tenderId, 409, "Bid operation journal requires review.");
            foreach (var reference in new[] { intent.NewReference, intent.OldReference }.Where(x => x is not null).Distinct())
                if (!await data.ReferenceExistsAsync(reference!, ct)) await files.DeleteAsync(bidId, reference!, ct);
            await files.CompleteIntentAsync(intent, ct);
        }
    }
    public async Task RecoverAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default)
    {
        var preliminary = await bids.Read(user, bidId, ct);
        await using var tx = await data.BeginAsync(preliminary.Bid.TenderId, ct);
        var current = await bids.Read(user, bidId, ct);
        await RecoverLockedAsync(bidId, current.Bid.TenderId, ct);
        await tx.CommitAsync(ct);
    }
    public async Task<BidDocumentDownload> DownloadAsync(ClaimsPrincipal user, Guid bidId, Guid documentId, CancellationToken ct = default)
    {
        Stream? content = null;
        try
        {
            var preliminary = await bids.Read(user, bidId, ct);
            await using var tx = await data.BeginAsync(preliminary.Bid.TenderId, ct);
            var snapshot = await bids.Read(user, bidId, ct); // Same query-level role/tender/stage restrictions as bid contents.
            await RecoverLockedAsync(bidId, snapshot.Bid.TenderId, ct);
            var document = snapshot.Documents.SingleOrDefault(x => x.Id == documentId && x.BidId == bidId) ?? throw new BidException(404, "Document not found.");
            content = await files.OpenVerifiedAsync(document, ct);
            await tx.CommitAsync(ct);
            var result = new BidDocumentDownload(content, document.OriginalName, "application/octet-stream", document.Size);
            content = null; return result;
        }
        catch (InvalidDataException) { throw new BidException(409, "The private document failed verification."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new BidException(503, "The private document is temporarily unavailable."); }
        finally { if (content is not null) await content.DisposeAsync(); }
    }
}
