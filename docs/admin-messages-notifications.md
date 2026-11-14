# Admin messages and notification dropdown

User request: an Admin messages section, dashboard message count, stacked notifications opening from the bell without immediate navigation, and a read state.

## Implemented

- `/Messages` lists the signed-in Admin's Contact messages, with subject, received time, read/unread state, totals and pagination (25 per page). `/Messages/Details/{id}` displays the complete plain-text sender information and message. The existing Contact delivery copies are reused; there is no new table or message duplication.
- Admin eligibility is checked both by the existing authorization policy and a fresh Identity role/lockout query. Supplier-role/profile accounts are excluded. Every message query also restricts RecipientUserId and the existing Contact prefix with no procurement links. Another Admin's or supplier's copy is never exposed.
- A Messages sidebar link and sixth dashboard KPI use the same recipient-scoped real database message filter. The KPI counts all Contact enquiries addressed to the current Admin; read messages remain part of that total.
- The bell is a native details/summary dropdown, showing the latest eight own notifications vertically, current unread count, read/unread labels and a View all link. Opening the bell does not navigate or mark everything read. The full notifications page is paginated.
- Mark as read uses an antiforgery-protected MVC POST. Vanilla JavaScript requests an HTML partial and updates the list and badge in place; there is no Web API/JSON endpoint. Native POST/redirect remains a no-JavaScript fallback. External return URLs are rejected. Escape/outside click close the dropdown, keyboard focus is preserved, and small screens use a bounded viewport popover.
- Opening a Contact message through Read message uses a protected POST, marks that own message read, then redirects to its details. Details GET is read-only. Repeated reads preserve the original timestamp: the SQL UPDATE is conditional on RecipientUserId, id and ReadAt IS NULL.
- Existing offer/tender/report destinations, role visibility, authorization and document isolation are retained. No reply, delete, mark-all, notification transport, role, entity or migration was introduced. Times display in Asia/Amman and read timestamps persist in UTC.

## Verification

- Build passed, zero warnings/errors. EF schema metadata remains at 37 required tables and six migrations.
- 21 database-free notification/message MVC/security checks passed: owner isolation, Admin-only messages, stale Admin eligibility, escaped content, CSRF, idempotent reads, real counter/partial binding and no-JavaScript redirect safety.
- 59 dedicated browser checks passed at 1440/1024/768/390/320 pixels: message KPI/sidebar, bell stays on the current page, vertical list, viewport bounds, Escape/focus, in-place unread updates, full message read flow and supplier isolation. No JavaScript errors.
- Regressions passed: 44 Admin dashboard browser checks, 36 Admin dashboard service/MVC checks, 45 workspace role/routing checks, 19 Contact checks and the existing database-free authentication/Employee/Department suite.
- SQL read-state/persistence checks are prepared behind the explicit `--inbox-sql-checks-only` command. They must not run without this task's separate approval. The fixture encloses all temporary users, notifications and read updates in a rollback transaction and verifies existing data fingerprints afterward.

The dropdown uses the current page's bounded notification snapshot; it does not add polling or background notification delivery. The View all page retrieves current data.

After separate user approval, `--inbox-sql-checks-only` passed all 18 SQL checks. Verified actual Contact persistence, matching Admin message/dashboard counts, recipient isolation, conditional read timestamp updates, unread counter changes, repeated-read idempotence, stale/mixed-role rejection and failed-save behavior. The transaction was rolled back completely; fingerprints confirmed existing users, role assignments and notifications matched their baseline. No test records were committed and no migrations were applied.
