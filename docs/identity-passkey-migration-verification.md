# Identity passkey migration application verification

The user explicitly approved applying only 20261115034155_IdentityPasskeyStorage. It was applied on 2026-11-15. This approval did not authorize temporary SQL test records, passkey registration or other migrations.

## Guarded procedure

`tests/Apply-IdentityPasskeyMigration.ps1` defaults to read-only preflight. Its explicit `-Apply` mode pins the reviewed normalized SQL SHA-256 to 828BD1893FD2176F875E87B8D2C443448BDC2CA08FB42ED518B17DECD834E55E, restricts the configured database to the existing integrated-security local STMS target, and requires exactly the seven previously approved migration-history entries with no passkey table already present. It refuses rerun/partial/conflicting state rather than overwriting it.

The application acquires an exclusive migration application lock in a serializable verification transaction. It captures every existing table's original ordered column projection, row count and SHA-256 JSON content fingerprint entirely within SQL Server. Only aggregate verification results are printed; user identities, credentials and actual record contents never leave the server through the script output.

Exactly the reviewed SQL runs within the outer transaction. Verification checks the three required non-null column types, unique credential primary key, user lookup index, enabled/trusted account foreign key without cascade deletion, an empty new table, exactly one added migration-history row, unchanged earlier migration-history records and unchanged inventory/counts/content fingerprints of the 38 pre-existing tables. Any mismatch rolls back the entire operation before commit.

## Observed results

- Read-only preflight passed before applying: seven applied migrations and no existing passkey table.
- Applied only 20261115034155_IdentityPasskeyStorage.
- AspNetUserPasskeys was created with CredentialId varbinary(1024), UserId nvarchar(450) and Data nvarchar(max), all NOT NULL; required indexes/foreign key verified.
- New passkey table contains zero rows.
- All 38 pre-existing business/Identity tables retained identical record counts and content fingerprints.
- Follow-up independent read-only schema inspection: 15 users, 7 roles, 8 applied migrations, passkey table exists, approved migration present once.
- No account/role changes, test users, seeds, biometric images/templates or registered credential records were created.

## Scope boundary

This completes the approved storage migration only. Existing password login remains unchanged. Enrollment/removal, WebAuthn sign-in MVC/browser ceremonies, replay/origin/eligibility checks and device testing still need implementation. Face/fingerprint/PIN functionality is not claimed enabled merely because the table exists. No application restart or deployment was performed as part of this migration operation.
