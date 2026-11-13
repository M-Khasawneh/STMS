# Purchasing and Tender Business Workflows

## Scope

Implement this workflow as part of the full ASP.NET Core MVC application targeting .NET 10 (`net10.0`), with EF Core 10, SQL Server, Identity and Razor Views using HTML, CSS and JavaScript in Visual Studio 2026. Each stage includes its required pages, dashboards, validated forms and server-enforced authorization. No Web API is planned. The technical decision does not resolve missing business rules or change the source workflow.

This document records the workflow in source sections 2, 4, 8 and 10. Source paragraph locators appear in [requirements-analysis.md](requirements-analysis.md). The sequence is preserved: purchase request → review/approval → tender → supplier invitation → bid receipt → compliance → technical and financial evaluation → unified report → manager review → Top Committee where required → award and closure. No additional approval tier or changed workflow is adopted.

Source state lists specify allowed vocabulary but do not specify a complete transition graph. The transition tables distinguish **source-supported** business actions from **candidate mappings** of those actions to named statuses. Candidate mappings must be confirmed before implementation. Missing rules must not silently become defaults.

## Exact source status vocabulary

| Record | Source statuses |
|---|---|
| Purchase request | Draft; Submitted; Under Review; Approved; Rejected; Returned; Converted to Tender |
| Tender | Draft; Published; Open; Closed; Under Evaluation; Awaiting Manager Review; Awaiting Committee Decision; Awarded; Cancelled |
| Invitation | Pending; Accepted; Declined; Expired |
| Bid | Draft; Submitted; Under Review; Compliant; Non-Compliant; Accepted; Rejected; Withdrawn |
| Recommendation and decision | Recommendation Pending; Manager Approved; Manager Rejected; Returned for Review; Committee Approved; Committee Rejected |
| Compliance result | Compliant; Non-Compliant; Pending Clarification |
| Supplier registration/verification | Pending; Approved; Rejected; Suspended |
| Clarification | Pending; Answered; Resolved |

Pending Clarification is a compliance result, not a supplied Bid status. Closed occurs before evaluation in a plausible tender lifecycle, but the source also says close after recording the winner. Preserve both statements and ask how operational closure should be represented. Do not add a Completed status or equate Closed with Cancelled.

## Purchase request

The requisition reviewer, ownership, validation and transition questions were subsequently resolved by the user and implemented. See [purchase-requisitions.md](purchase-requisitions.md) for the confirmed contract and verification. Earlier candidate/open notes below are superseded for this module.

The requesting department identifies its need and records title, reason, priority, target date, items, quantities and specifications. Review can approve, return for changes, or reject. The department tracks status, history and review notes. Only approved requests feed tender creation.

| From → to | Action and responsible party | Evidence and guards | Interpretation |
|---|---|---|---|
| Draft → Submitted | Department submits | Required request/item information; department authorization | Candidate status mapping of source creation/review sequence |
| Submitted → Under Review | Reviewer begins | Authenticated reviewer; reviewer authority unresolved | Statuses given; exact transition is a candidate |
| Under Review → Approved | Reviewer approves | Decision, actor, date and notes retained | Approval expressly required; reviewer not identified |
| Under Review → Returned | Reviewer requests changes | Review notes visible to department | Return expressly required; required note format unspecified |
| Under Review → Rejected | Reviewer rejects | Decision/reasons/history retained | Rejection expressly required |
| Returned → Submitted | Department corrects and resubmits | Item/request validation repeated; earlier review preserved | Necessary candidate rework path, not explicitly defined |
| Approved → Converted to Tender | Sourcing Officer creates tender | Request is approved; preserve request-to-tender relationship | Source-supported conversion, cardinality unresolved |

Open rules: who approves; self-approval; review authority limits; whether approval can be withdrawn; rejected-request reopening; deleting drafts; multiple reviewers; whether conversion may split/combine requests; edits after approval. No budget reservation or budget approval workflow is specified.

## Tender creation and invitation

The user subsequently approved and the application implements the tender/invitation contract in [tenders-invitations.md](tenders-invitations.md). That contract supersedes earlier candidate/open notes below for conversion, Draft/Published/Open/Closed, criteria approval, invitation responses/expiry and invitation notifications. Later bidding/evaluation/award workflows remain outside this module.

The Sourcing Officer sets title, description, terms and submission dates, selects suitable suppliers and sends invitations. Criteria and weights must be pre-approved before opening. The source prohibits changes after receiving bids unless an official documented procedure is followed; the exception procedure and its authority are absent.

| From → to | Action/actor | Guard or unresolved point |
|---|---|---|
| Draft → Published | Officer publishes | Candidate mapping; approved source request, terms/dates and criterion approval; publication visibility undefined |
| Published → Open | Open submission period | Candidate mapping; whether scheduled or officer action is unspecified |
| Open → Closed | End bid receipt | Candidate mapping of the deadline requirement; clock boundary, time zone, extensions and automation unspecified |
| Closed → Under Evaluation | Begin evaluation | Candidate mapping; relationship to compliance-complete gate is unspecified |
| Under Evaluation → Awaiting Manager Review | Committee sends unified report | Directly supports source report handoff; report must contain required results |
| Awaiting Manager Review → Awaiting Committee Decision | Manager forwards when required | Source-supported conditional forwarding; high-value/critical policy missing |
| Awaiting Committee Decision → Awarded | Authorized collective decision selects supplier; winner recorded | Source-supported outcome; precise finalization actor and quorum unresolved |
| Awaiting Manager Review → Awarded | Potential route for non-forwarded tenders | **Unresolved**: source does not identify final award authority in this branch |
| Active status → Cancelled | Cancel tender | Only the status is supplied; actor, permitted origins, reasons and consequences unresolved |

Source does not define tender amendments, re-opening after closure, bid opening ceremonies, public tender access, or cancellation after award. Do not implement these by assumption.

Invitations link a tender and supplier. Supplier sees and accepts/declines its invitation. Pending → Accepted and Pending → Declined map directly to these actions. Pending → Expired is suggested by the status list, but expiry rule and timing are not given. Whether acceptance is a prerequisite to bid submission, responses can change, invitations can be resent, or suspended suppliers can remain invited must be clarified. Supplier selection criteria and minimum supplier count are also unspecified.

## Bid submission

Supplier submits prices, technical details, delivery timeframe and documents before the deadline. Financial review also needs item quantities, taxes, fees, applicable transport/installation/maintenance, payment terms, currency and offer validity. The source does not prescribe which of these are structured input fields versus extracted offer details.

| From → to | Action/actor | Interpretation and guard |
|---|---|---|
| Draft → Submitted | Owning approved supplier submits | Submission before deadline is explicit; invitation prerequisite and one-bid limit are open |
| Submitted → Under Review | Authorized internal review | Candidate mapping; exact opening/read timing is unspecified |
| Under Review → Compliant | Officer completes compliance | All mandatory requirements met |
| Under Review → Non-Compliant | Officer finds mandatory failure | Clear reasons must be logged |
| Compliant → Accepted/Rejected | Later business outcome | Source statuses only; whether Accepted means evaluation acceptance or award is unresolved |
| Draft/Submitted → Withdrawn | Supplier withdrawal | Source status only; origins, deadline and resubmission rules unresolved |

Edits/replacement bids, partial item offers, alternative offers, discounts, multi-currency comparisons, bid revisions and post-deadline supplementary files are not defined. No late-submission exception is granted. A Pending Clarification compliance result must be represented separately until its relation to Bid.Status is agreed.

## Compliance check

Sourcing Officers verify mandatory documents and essential terms. Outcomes are Compliant, Non-Compliant with clearly logged reasons, or Pending Clarification. Requests for missing documents flow between officer and owning supplier. Preserve document evidence, reviewer, date and rationale as supporting data; exact storage is a design suggestion.

The source does not say whether noncompliant bids are automatically excluded, who may reverse compliance, what must be complete before evaluation starts, or whether a clarification can cure an essential requirement after the deadline. An internal technical observation of noncompliance must not silently replace an officer's compliance result.

## Technical and financial evaluation

The four evaluation positions are Sourcing Manager, Department Representative, Financial Representative and Technical Representative. All proposals are evaluated against established criteria/weights. Technical tasks include documents, images, videos, samples, scoring, observations, noncompliance, necessary clarifications and approved technical results. Financial tasks include item prices/quantities, tax/fees, transportation/installation/maintenance when applicable, payment terms, currency, validity and total comparison.

The source does not specify whether all four score every criterion, whether technical evaluation precedes financial access, minimum passing scores, normalization, individual-score aggregation, tie-breaking or cost formula. Record these as open rules. Do not substitute “lowest price wins.” Technical and financial scores are required but no formula is supplied.

## Unified evaluation report

Committee prepares one combined report for the manager. It must include technical and financial scores, observations, noncompliance, supplier comparison, final result and substantiated recommendation. Report tracking belongs to the officer; report preparation belongs to the committee. Approval of technical results is required; the unified report's authoring/sign-off procedure is unspecified.

Suggested integrity behavior for later approval: retain the evaluated inputs and submitted report version so subsequent decisions remain explainable. Do not overwrite evidence after review. This does not prescribe a new approval step or document-signature technology.

## Manager review and Top Committee

| Outcome | Source behavior | Unresolved consequence |
|---|---|---|
| Manager Approved | Approves recommendation; forward high-level tenders when policy requires | How ordinary tenders receive a final award; forwarding threshold and authority |
| Manager Rejected | Rejects recommendation with comments | Whether tender closes, is cancelled, or returns to evaluation |
| Returned for Review | Requests changes with comments | Destination state, re-evaluation scope, report revision and reapproval |
| Committee Approved | Final collective supplier selection with documented notes/justification | Voting/quorum, tie/abstention/conflict rules; whether it may select a different supplier |
| Committee Rejected | Source decision status exists | Subsequent tender state and possible resubmission |
| Further details requested | Top Committee may seek information | No corresponding separate decision status supplied; route through manager clarification pair and return path need confirmation |

Top Committee has four members. It can access requisitions, submitted bids, unified reports, manager recommendations, technical/financial outcomes, available AI analysis and past decisions. Voting records are expressly reportable even though voting mechanics are absent. A collective decision must not be invented from an arbitrary majority rule.

## Award and closure

Record the selected supplier, close the tender, and notify all parties. Preserve the decision, justifications and awarded-contract reporting data. The source does not require contract drafting, purchase orders, delivery receipt, invoices, payment execution or contract lifecycle management. Supplier performance reporting is required but its source measures are unknown; do not invent a downstream fulfillment system.

Resolve Awarded versus Closed representation, multiple-winner/line-award capability, authorized award recorder, notification recipients and public disclosure before implementation. No cancellation/reversal of an award is specified.

## Clarifications and notifications

Allowed pairs are detailed in [roles-permissions.md](roles-permissions.md). The system automatically identifies allowed recipients using role, involvement and stage. Sender records subject/detail and a related request, tender or offer. Receiver responds; sender is notified; all messages and dates remain in the transaction history.

Pending → Answered maps to response receipt. Answered → Resolved maps to resolution, but the person authorized to resolve and any reopen/multiple-response behavior are undefined. New question and response notifications are mandatory. Delivery channel, delivery retries, read indicators and notifications for other state changes are not prescribed except award/closure notifications.

## Audit and edge-case verification

Maintain a clear audit of every offer, approval and decision as required by P030/P106/P209. Proposed event content is actor, role context, record reference, action, timestamp, old/new status, reason and evidence reference. Keep credentials, raw biometrics and confidential document contents out of ordinary diagnostic logs. Audit read access, retention and correction procedures need approval.

Planned test cases include simultaneous edits or decisions; duplicate submissions; a bid arriving exactly at deadline; supplier suspension during submission; expired required legal documents; zero bids; all bids noncompliant; missing committee member; member replacement; manager acting as evaluator/reviewer; unanswered clarification; changed report after return; AI unavailable; notification failure; interrupted uploads; cancelled tender with submitted bids; and orphaned request-to-tender relationships. Where expected outcomes lack a source rule, the test is a question pending business decision, not an invented pass condition.
