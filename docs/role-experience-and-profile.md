# Role workspaces and own-profile settings

## Delivered scope

- Built-in protected `admin` is excluded from employee listings and employee KPI counts. Its account, department, roles and direct-edit protections remain intact. Other employee accounts with Admin assignments retain their existing behavior.
- Employee Create/Edit multi-role controls have a keyboard-accessible dropdown with checkbox choices. The original MVC multi-select remains the submitted field and fallback without JavaScript. Supplier is excluded. Existing roles remain selected; editing them requires the existing explicit role-change checkbox.
- Admin retains its existing guarded dashboard. Other roles receive role-specific, read-only dashboards using real EF queries: own requisitions; sourcing review work excluding the officer and their department; unapproved criteria; supplier-owned invitations/offers; and effective tender-specific committee assignments at appropriate stages. Multi-role employees receive their applicable sections. No evaluation, award or internal bid-review actions have been introduced.
- The existing Admin shell now applies to all authenticated workspaces. Role-aware navigation and original action permissions remain authoritative. Notification access uses a header bell linking to the existing inbox, with no fabricated unread badge or new notification workflow.
- Own-profile settings allow email, phone and password changes only. Password change requires the current password and matching new-password confirmation, and uses Identity validation/hashing. No editable account identifier, username, roles, department or company details are accepted. Both POST actions require antiforgery validation. Password values are cleared on failed responses. Changes use transactions and refresh the current Identity cookie; security-stamp changes invalidate other sessions according to existing cookie validation settings.
- Profile settings open in the existing same-origin form dialog and keep server/client validation. Direct navigation remains available.
- About and Contact now have conventional Home controller GET endpoints for their existing views. Public navigation links to these dedicated pages from Home/About/Contact. Existing homepage effects and the six-stage workflow remain. Real public statistics bindings are restored, with unavailable values explicitly distinguished from zero. Contact details remain unconfigured; no address or submission workflow is invented.
- Privacy is linked only from anonymous Home. Existing Privacy endpoint remains. Login now uses the Navy/Blue/Cyan identity with a prominent Supplier Registration action. Authenticated Home continues redirecting to Workspace.

## Safe verification

- Solution build including compiled Razor: succeeded, zero warnings/errors.
- `--experience-checks-only`: 16 HTTPS/Identity profile checks passed, plus connection-free query translation checks for ownership, officer review exclusions, supplier offer scope and effective committee/tender-stage restrictions.
- Default HTTPS authentication runner: passed, including employee/department management, protected first Admin behavior, multi-role preservation, antiforgery, cookie/session checks and login lockout. Uses memory Identity stores only.
- `--workspace-ui-checks-only`: 45 routing/role checks passed.
- `--admin-dashboard-checks-only`: 36 checks passed.
- `--bid-phase3-checks-only`: 61 MVC, 97 private-document and 92 bid-domain checks passed, together with existing tender/requisition rules and supplier upload checks.
- `tests/experience-browser.cjs`: 206 checks passed against explicitly marked memory fixtures. Public pages checked at 1440/768/390/320px, all seven role shells at 1440/390/320px, mobile menu/Escape, profile fields, role dropdown, Privacy visibility, reduced motion and zero JavaScript errors. Screenshots are local external QA artifacts; role-dashboard screenshots use synthetic fixture models, not production SQL counts.
- `tests/workspace-ui-browser.cjs`: 54 checks passed, including real MVC memory-backed employee save with two roles, explicit role-update gating, modal validation, private upload/download and final offer submission.

No SQL write tests, migrations, seeding, dependency installations or production account changes were executed. Browser fixtures were stopped and their temporary private storage removed. Live SQL dashboard totals and production profile persistence have not been exercised by this test pass; new role dashboard scopes are verified through SQL translation without opening a database connection.

## Remaining boundaries

Committee dashboards summarize existing authorized assignments only; evaluation and final decisions remain future modules. Contact information still requires real configuration. Existing notifications are reused without additional delivery/read-state functionality. No database entity or migration changed.

## Browser reproduction

Use the existing installed Playwright/Edge runtime. `--workspace-ui-browser-fixture` supplies the marked memory-only role host and a tender ID. `--experience-browser-fixture` supplies a separate HTTPS memory Identity host and requires an ephemeral `STMS_UI_TEST_PASSWORD` environment value. Run `tests/experience-browser.cjs <https-fixture> <role-fixture> <external-output-directory>` with the same ephemeral password and `STMS_PLAYWRIGHT` if needed. Only local generated test certificates are bypassed by that browser fixture; production TLS/authentication configuration is unchanged. Stop both hosts afterward. Never target production with these test scripts.
