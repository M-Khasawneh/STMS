# Private bid document extraction and OCR

## Delivered behavior

The user approved PdfPig, PDFtoImage, TesseractOCR and their necessary dependencies plus English/Arabic OCR language data. The existing local Ollama advisory module now receives extracted document excerpts, not just attachment inventory. No controllers/actions, roles, procurement decisions, upload policies, database entities or migrations were added or changed for this extension.

- PdfPig reads native PDF text. Image-containing or sparse-text pages are rendered with PDFium through PDFtoImage and read with Tesseract eng+ara. PNG and JPEG are read with Tesseract directly after image-header bounds checks.
- Original files stay private and byte-for-byte unchanged. AiBidDocuments reuses fresh BidReviewPages/IBidData reviewer, tender and stage scopes and IPrivateBidFiles.OpenVerifiedAsync, including existing signature, size and content-hash verification. It never accepts posted physical paths or supplier IDs. Draft/unavailable bid records are not exposed.
- Provenance records bid ID, document ID/type, verified-byte SHA-256, page number, method, OCR confidence and the exact bounded excerpt supplied to AI. Per-page fact keys are included in the model's existing constrained schema and server citation validation.
- Extracted text is untrusted evidence, never an instruction. Server prices and quantities remain authoritative; model advice must highlight discrepancies rather than rewrite business facts. OCR confidence is engine confidence, not proof of accuracy or compliance.
- The existing advisory page displays extraction coverage, page excerpts, confidence, unknown/unreadable pages and shortened/omitted text. All extracted text is Razor-encoded. User access and private file hashes are checked again before showing cached excerpts or saving analysis.
- A tool failure or timeout does not become a cacheable successful result: generation returns a safe retryable error without saving. An intrinsically unreadable/encrypted/no-text document may be recorded as uncertainty alongside the readable evidence. It is never treated automatically as a missing mandatory attachment or a compliance decision.

## Runtime isolation and bounds

Native parsing/OCR runs in a private CLI mode of the same ASP.NET Core application, --extract-bid-text, before WebApplication.CreateBuilder. It does not initialize MVC/Identity/database services. This is not a new web endpoint, service deployment or frontend framework.

The parent starts only the trusted application assembly through dotnet with UseShellExecute=false and CreateNoWindow=true. Verified bytes travel through stdin; bounded result JSON returns through stdout. Uploaded files are never executed or passed as command lines. No private source images, converted PDF pages, raw parser errors or extracted-text logs are written to temporary disk.

- Existing maximum source file size: 5 MB. At most 20 attachments are processed in one analysis; larger jobs fail clearly and human review remains available.
- At most 10 pages per PDF, 1200 extracted characters per processed page. Actual total/processed counts and excerpt truncation are retained. JPEG/PNG dimensions must be <=6000 per side and <=12 million pixels before OCR decode.
- PDF rendering preserves aspect ratio within approximately 2000x2600 px at up to 150 DPI. Annotations/form-fill rendering is disabled. This is bounded text extraction, not a guarantee of full visual/semantic reproduction of every PDF feature.
- Each worker has a 45-second deadline, bounded stdout/stderr and a polled 512 MB working-set guard. Cancellation or failure kills/waits for the child; outstanding pipe tasks are cancelled/observed. The memory guard is best-effort process monitoring, not a hard OS sandbox or antivirus system.
- The extraction phase has a two-minute budget. Existing Ollama generation has its separate three-minute bound and per-app single-operation gate. UI describes up to five minutes. A reverse proxy/deployment must permit the chosen MVC request duration.
- Combined model evidence remains within 16,000 serialized UTF-8 bytes. Coverage records are reserved first; page excerpts are shortened/omitted to fit, explicitly marked, and the precise resulting excerpts are preserved. If coverage metadata itself cannot fit, analysis fails clearly. No offer is silently dropped.

## Persistence and caching

AiBidAnalyses.AnalysisJson now stores a version-2 envelope containing validated advice and the exact document evidence used. No new SQL column is required. An extraction-profile/version plus stable attachment identity/reference fingerprints is part of the base source hash, so earlier inventory-only results are not reused or deleted.

OCR and model inference run outside the SQL transaction. Saving reuses the existing serializable tender lock, fresh source/actor checks and atomic analysis/audit insert. It verifies document ownership, manifests and private-byte hashes without rerunning native OCR in the transaction. Excerpts are reconstructed into the exact citation facts when reading. Source changes or private-file tampering deny reuse, including cached results.

## Setup and deployment

- Approved packages: PdfPig 0.1.16, PDFtoImage 5.4.0 and TesseractOCR 5.5.2. See third-party/document-extraction-licenses.md for licenses and transitive native components.
- Native PDF rendering/OCR was verified on Windows x64 with the existing Visual C++ runtime. Other OS/native deployments are not claimed tested; the current PDF rendering guard is Windows-only. Managed PDF text extraction does not require OCR language data.
- Run `pwsh -File scripts/Install-OcrLanguageData.ps1` explicitly after approving the download on a new deployment. Optional -ContentRoot selects the deployed web application root. This downloads only pinned official eng/ara tessdata_fast assets, validates SHA-256 and refuses differing existing files or linked asset paths. It makes no SQL calls and is idempotent.
- Default language location: ContentRoot/App_Data/Ocr/tessdata, outside wwwroot. A trusted server BidText:LanguageDataPath override is supported. Normal application startup never downloads or installs anything.
- Language binaries, native build artifacts, test images/screenshots and raw uploaded documents are not committed. The local approved setup was verified twice by its installer checks without overwriting existing files.

## Verification

- Solution and compiled Razor build: zero warnings/errors.
- --document-extraction-checks-only: 28 database-free checks with actual native tools and private temporary synthetic files. Covers native PDF text, PNG/JPEG OCR, PDFium scanned-PDF OCR, Arabic recognition from a shaped synthetic image, encrypted/malformed PDF, missing language data, page/dimension/context bounds, stable citation reconstruction, invalid/null provenance, initial/active cancellation, verified private SHA-256, Admin/Supplier/tender isolation, file tampering, and retryable OCR failures. Temporary private fixtures were cleaned safely.
- --ai-document-model-checks-only: actual local qwen3:4b analyzed text extracted from a synthetic PDF and returned a valid document/page citation. No SQL, real supplier data or private production files used.
- Existing AI checks: 22 schema/transport and 26 service/compiled MVC checks passed with deterministic memory fixtures.
- AI browser fixture: 24 Edge checks passed, including encoded document excerpts, page/coverage UI, generation, antiforgery, role isolation and no overflow at 1440/768/390/320 px. Native OCR and actual Ollama were tested separately from this deterministic browser fixture.
- Regression checks passed: bid activity/internal document visibility 54; manager report 40+29; Top Committee 128+49+23. SchemaChecks still verifies seven migrations and 37 required tables.
- Existing SELECT-only SQL runner: 100 read/report/export checks passed, including new-profile AI metadata/history queries. It did not open real supplier document bytes, invoke OCR/Ollama, insert analysis or change database records.
- NuGet direct/transitive vulnerability query returned no listed advisories at check time. This does not guarantee native parsers are vulnerability-free.

## Remaining verification limits

No SQL write/persistence/concurrency test was run or authorized by the dependency approval. Existing records and migrations were untouched. Broader layout/OCR accuracy, handwriting, rotated/low-quality material, unsupported PDF annotations/form fields, large jobs and production deployment resource behavior still require human/operational verification. OCR/AI never replaces the approved human compliance, evaluation, manager, committee or award workflows.
