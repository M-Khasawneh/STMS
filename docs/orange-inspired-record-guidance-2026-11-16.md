# Orange review and independently implemented record guidance

## Evidence and scope

Reviewed the actual V5 Word document in the STMS root, existing STMS controllers/views/client scripts and the public Orange repository. The source snapshot is Orange `main` at `f9d5b73a8889197a2ee8a2006c96998206f7b779`. Enumerated 21 remote branch heads, inspected the current main UI/workflow changes and selected branch history; this is not a claim of a complete code/security audit of every branch. The external application, installers and migrations were never executed.

No application-source license was found in the inspected main tree; the visible LICENSE files belong to bundled third-party libraries. No Orange code/assets were copied or dependencies installed. The following UI concepts were implemented independently using STMS's existing Razor/HTML/CSS/JavaScript, actual models and approved business rules.

## Useful ideas and STMS adaptations

| Orange idea and verified source | Benefit and limitation | Independent STMS implementation / V5 alignment |
| --- | --- | --- |
| Visible process phases: [`_WorkflowStepper.cshtml`](https://github.com/Tender-Management-G2/Orange-Tendering-System/blob/f9d5b73a8889197a2ee8a2006c96998206f7b779/Orange-Tendering-System/Views/Shared/_WorkflowStepper.cshtml) | Helps users locate a tender in the process. Orange's four weighted-cycle phases and its final-state completion inference differ from STMS; copying them would misrepresent our statuses. | New read-only `_TenderProgress` on Tender Details and the supplier's existing tender workspace. Highlights only the exact stored STMS status; handles Cancelled separately, adds no stage actions, invented percentages or inferred approval history. Uses V5 section 8 and the approved explicit transitions. |
| Organizing a long transaction into sections: [`_WorkflowPopup.cshtml`](https://github.com/Tender-Management-G2/Orange-Tendering-System/blob/f9d5b73a8889197a2ee8a2006c96998206f7b779/Orange-Tendering-System/Views/Shared/_WorkflowPopup.cshtml) | Related content is easier to reach. Wholesale adoption would import Orange's state machine, signing requirements and combined modal structure; hiding sections can also hide validation/context. | An opt-in “On this page” anchor navigator for Tender Details, Supplier Portal, supplier/internal offer details and the unified report. Uses only headings already visible to the current user; original content, forms and native history remain present. V5 sections 4/12 readability and authorized presentation. |
| Placing document selection next to the relevant transaction's upload action: same popup's upload section | Useful context before a submission, even though Orange accepts formats not approved for STMS. A selected filename is not proof of a valid or uploaded file. | Shared local filename/byte-size feedback and Clear selection for existing file inputs. Original accept types, required fields, maximum limits, signature/ownership checks and explicit POSTs remain authoritative. No file reading, inline PDF execution, automatic upload, external processing or server-document deletion. V5 sections 4.5/6/12 and the approved supplier/bid-document policy. |

STMS already implements the useful central-work/search, role dashboards, popup forms, private documents, side-by-side offer comparison, in-app notifications, audit, reports and protected exports. Those modules were preserved rather than rebuilt. Orange's `_InternalDashboard`, `_WorkflowList`, `_PageHeader`, `_EmptyState` and `_ConfirmAction` were also examined for organization/feedback ideas. Some shared helpers are explicitly scaffolded; that does not invalidate their UI ideas, but hardcoded placeholders such as the old `_ApprovalTracker`'s `0 / 4` must not replace STMS's real voting data.

## Deliberately not imported

- Signature-pad/report-signing storage: STMS selected QR and watermarking as its optional features. A third signing workflow, storage schema and legal/security interpretation were not authorized here.
- Meetings/Zoom/Jitsi: not an implemented STMS requirement or approved service dependency.
- Automatic committee seating: conflicts with the existing explicit manager appointments, tender-specific membership and recorded voting-round snapshots.
- Orange default weights, approval levels, statuses and reset/demo tools: STMS's Word document and approved decisions remain authoritative. No source-project workflow, data seed, reset mechanism or hosted AI guide was transplanted.

## Implementation and safety

- New shared `_TenderProgress.cshtml`, `record-guidance.css` and `record-guidance.js`.
- Existing shared layout loads the small enhancement assets on internal pages. `data-record-navigation="true"` is explicit and restricted to the five selected details screens; unrelated dashboards, lists and forms do not acquire section navigation.
- Supplier Registration opts into the same selection feedback through its existing Styles/Scripts sections. Supplier-registration documents, bid documents and profile pictures remain distinct existing server workflows.
- Uses textContent for heading labels and filenames; preserves existing labels/aria-describedby, keyboard focus and no-JavaScript content. Scrollable stages and outlines stay inside their containers. Print suppresses navigation/selection controls.
- No production controller, service, entity, DbContext, Identity/authorization rule, migration, database record, route/action or Word document changed.

## Verification interpretation

The dedicated `record-guidance` suite runs on marked memory/presentation fixtures and aborts every POST. It checks exact saved stages, current-state uniqueness, no mutation controls, native section navigation/focus/history, mobile containment, Admin/supplier presentation, safe filenames/real sizes, clear selection, unchanged upload field/type bindings, no automatic upload and no-JavaScript fallback. It is not a SQL-persistence or file-signature-validation certification; existing server security/regression suites provide their separate evidence.

Full UI verification caught a real integration issue: Razor renders empty data-* attributes, so selecting merely by attribute presence enabled outlines on unrelated pages. It was corrected to an explicit true/false value and strict opt-in selection, with a non-opted-in profile regression. Affected dashboard/notification checks were rechecked after the correction and passed. Registration loads the asset once for signed-in/anonymous layouts; initialization is also idempotent, verified by deliberately reloading the enhancement in the test.

Final evidence: solution/Razor build passed with zero warnings/errors; all 22 application JavaScript files passed syntax checks. All 24 safe-acceptance groups passed. All 24 browser groups passed after targeted integration rechecks, including the original bid uploads/downloads, report workflows, supplier portal and popup forms. The final dedicated presentation test passed 199 assertions with zero JavaScript errors and zero POSTs. The two initial dashboard/notification failures were corrected, not ignored. No production SQL mutation or external-repository execution is implied by these results.

QA logs and synthetic-fixture screenshots are outside Git under `orange-ideas-qa`. Physical device recognition, browser speech-service accuracy, actual external Orange runtime behavior and production SQL mutations are not claimed by this pass.
