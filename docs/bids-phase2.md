# Supplier bids: Phase 2 private documents

## Delivered scope

`BidDocumentManagement` provides service operations for upload, replacement, deletion, protected download, and cleanup retry. No controllers, views, public file routes, audit/notification features, or new dependencies are introduced. Existing `BidDocument` columns and relationships suffice; no migration is required or applied.

Production DI now resolves `IBidSubmissionDocuments` to `BidSubmissionDocuments`. This supersedes the historical Phase 1 pending-verifier registration described in `bids-phase1.md`. The legacy pending implementation remains fail-closed when explicitly selected in tests.

## Rules and access

- Exact document categories reuse the existing metadata convention: `Technical Proposal`, `Financial Proposal`, `Supporting Document`. At least one Technical Proposal is required for submission; other categories are optional; maximum ten documents total.
- Upload, delete, and replacement reuse Phase 1 ownership, approved/unlocked Supplier account, accepted invitation, approved criteria, Draft state, Open tender, and submission-window checks. Account and time are checked again before commit. Replacement at the ten-document limit is supported. Removing the last Technical Proposal while Draft is allowed, but submission then fails.
- Every document mutation uses the existing tender Serializable/UPDLOCK/HOLDLOCK transaction boundary and bid content-version check. Files are never overwritten in place. A replacement retains the document record ID and receives a new storage identifier. Document metadata participates in the bid version, preventing stale edits/submissions.
- Downloads reuse the existing bid query authorization before loading document metadata and recheck inside the transaction: eligible suppliers see their own documents only; officers/managers see submitted bids after closure; evaluators need an effective assignment to that tender; Top Committee members need the assigned final-decision stage (including V5 awarded history). Admin and Requesting Department roles do not grant confidential access. Internal users cannot read drafts.

## File protection

- Files live in `App_Data/BidDocuments/<bid-id>/`, outside `wwwroot`, separate from supplier-registration documents. The constructor rejects a storage root under the configured web root. Deployment must retain private filesystem permissions and must not map App_Data through another web server/static-file alias.
- Generated references contain a bid identifier, 256 random bits, and a SHA-256 content digest; original filenames are display metadata only. References are strictly validated, bid-bound, and reject traversal, absolute paths, registration references, and linked paths within the private storage tree.
- The existing bounded upload validator accepts only PDF/JPG/PNG with matching byte signatures, up to 5 MiB (5 x 1024 x 1024 bytes) per file. It rejects empty uploads, invalid/control-character filenames, malformed signatures, and streams inconsistent with their declared length. Client MIME headers are ignored.
- Submission and download reopen every requested stored file, check its actual length, rerun extension/signature validation, verify canonical filename and MIME metadata, and compare its content digest. Missing, altered, or mismatched files fail closed. Signature validation is not full document parsing or malware scanning; no scanning software is added.
- Downloads return bounded, verified, read-only bytes, never a storage path. `BidDocumentDownload` supplies an attachment-only contract with octet-stream MIME and no-store/nosniff/CSP headers. Phase 3 MVC actions must apply those headers, use attachment disposition, and dispose the returned stream correctly. No HTTP download endpoint exists in Phase 2.

## File/database failure handling

Before writing bytes or changing metadata, storage writes a flushed intent to a private per-bid `.pending` directory. New bytes use a temporary `.upload` file and an atomic rename. Metadata changes commit under the tender lock; old bytes are retired only after querying committed database references in a new locked transaction.

Recovery never assumes a thrown commit means rollback. It deletes only references absent from the database, preserving current documents even after a lost commit acknowledgement. Rollback, partial-file writes, cancellation, failed replacement, and post-commit delete failures are covered by focused checks. A durable intent is removed only after reconciliation succeeds. Incomplete `.tmp` intents are safe to remove because the protocol writes no document bytes until the intent rename completes.

If database/reference checks or filesystem cleanup are unavailable, the service logs the pending operation and retains its journal for retry rather than deleting uncertain files. The next authorized document operation/download, or explicit `RecoverAsync`, retries reconciliation. There is no background recovery worker. A persistent outage, inaccessible bid, or no later activity requires an authorized operational retry. This is recoverable coordination, not a distributed atomic filesystem/SQL transaction. Deployment must keep the journal and files together on durable private storage; all instances must use the same storage and SQL locking boundary.

## Safe verification performed

- Solution build using an isolated artifacts directory: passed, zero warnings/errors.
- `--bid-phase2-checks-only`: 97 Phase 2 checks passed using actual isolated temporary files and a transactional memory database double; 92 Phase 1 checks also passed, plus existing tender/requisition rule and supplier-upload checks. The runner returns before starting hosts or selecting SQL suites.
- Coverage includes file boundaries/signatures, actual-file submission verification, metadata/hash tampering, private references, draft immutability, accepted-invitation/eligibility/deadline checks, supplier isolation, role/tender/stage download restrictions, stale versions, simulated concurrent operations, rollback, ambiguous commits, cancellation, and journal recovery.
- Schema runner with no flags: existing four migration definitions and 36-table EF model passed; it did not connect to SQL Server.
- No database commands, migration application, seed/account creation, or database-changing tests were run. Filesystem test fixtures were restricted to generated OS temporary directories and removed after each check. Real SQL persistence/locking, HTTP antiforgery/download behavior, and browser UI remain unverified in this phase and require their separately authorized work.

Phase 3 (MVC pages, form validation/antiforgery, downloads and Asia/Amman presentation) remains pending explicit approval. Audit/notifications and broader integration verification remain in later approved phases.
