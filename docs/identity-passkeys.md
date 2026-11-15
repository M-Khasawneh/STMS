# Identity passkeys — MVC sign-in implemented

## Current implementation

The approved face/fingerprint/PIN device-sign-in integration is now implemented using native .NET 10 Identity and browser WebAuthn. Password login remains available. Physical face/fingerprint/PIN verification has not been demonstrated; virtual authenticators verify the protocol, not actual biometrics. STMS does not identify which local method was used and stores no face photos or fingerprints.

- Account/Login adds explicit passkey sign-in. Profile settings links to Passkeys/Index for account-scoped registration/removal.
- Registration requires current password and a name of at most 80 characters, with at most five keys per account. Removal requires current password and ownership. No account creation or passkey-only registration is added.
- Native SignInManager validates attestation/assertion and performs sign-in with existing EmployeeSignInManager eligibility, lockout and supplier/account restrictions. Discoverable login options do not enumerate usernames.
- All options/completion/removal/cancel actions are MVC POSTs with antiforgery and no-store headers. Native Identity handles cryptographic verification. User verification and discoverable credentials are required.
- An atomic one-use server ticket supplements Identity's protected temporary cookie. It binds operation, challenge, enrollment user/security stamp/name and a three-minute expiry. Completion consumes it before verification and checks the client challenge. Restored cookies cannot replay a completed request. A bounded 2000-entry registry and write gate support the current single application instance; distributed deployment requires shared atomic state and coordination.
- Passkeys:Origin must exactly match the HTTPS scheme/host/port. The RP hostname is explicit; cross-origin iframes and mismatched origins are denied. Development defaults to https://localhost:7162. Production requires explicit private configuration, for example Passkeys__Origin. Password login remains available when configuration is absent. Existing real settings were not modified.
- Rate limit: 20 passkey requests/IP/minute. Request bodies are bounded to 32 KB and credential JSON to 24000 characters. No new libraries, SDKs, entities or migrations were added in this implementation.
- Vanilla JavaScript invokes real device prompts, supports cancellation/Escape, clears submitted passwords, prevents double submission and uses server-selected same-origin destinations. Unsupported browsers and no-JavaScript mode retain password login. Device face/fingerprint/PIN choices depend on the authenticator.

### Current verification

- Build and compiled Razor: zero warnings/errors. JavaScript syntax checks passed.
- --passkey-checks-only: 16 memory-only ticket, expiry, concurrency, origin, native options, antiforgery, malformed input and password/reauthentication checks passed.
- tests/passkey-browser.cjs: 27 Edge checks with a virtual authenticator and real native Identity cryptography passed: registration, signed assertion/login, restored-cookie replay refusal, cross-account removal isolation, password-confirmed removal, removed-key rejection, encoding, unsupported/no-JS fallback and 1440/768/390/320 px layouts without overflow or JS errors.
- tests/passkey-security-browser.cjs: 10 additional checks passed: altered client origin/challenge, reused request, signed assertion without user verification, disabled account and revoked eligibility were rejected.
- Existing default HTTPS Identity/employee/department regression suite and 59 workspace role/navigation checks passed using memory stores.
- SELECT-only actual SQL runner: 106 read/report checks passed, including native UserManager.GetPasskeysAsync for six existing eligible role accounts. No credential registration/removal/counter update occurred in SQL. No eligible TopCommittee account was available for this runner.

Actual SQL credential write/concurrency tests, physical device demonstrations and production-origin deployment verification remain outstanding. No test accounts or credentials were created in the user's database. The previously approved migration remains applied; no new schema changes were performed.

The following sections record the earlier storage preparation; their statements about endpoints not yet enabled describe that earlier increment only.

The user approved passkeys supporting face/fingerprint on compatible devices and allowing PIN. The system must not claim to identify which biometric modality was used, accept static face uploads, or store raw face/fingerprint data. Physical face and fingerprint verification remains a device demonstration requirement; a virtual authenticator or PIN-only demonstration does not prove both modalities.

## Prepared migration

20261115034155_IdentityPasskeyStorage adds only AspNetUserPasskeys:

- CredentialId varbinary(1024), primary key: globally unique credential identifier.
- UserId nvarchar(450), required foreign key to existing AspNetUsers, indexed; no cascade deletion.
- Data nvarchar(max), required Identity-owned JSON: public-key credential and authenticator metadata, not raw biometric images/templates.

The EF mapping deliberately preserves the existing Identity V1 user, login and token columns rather than globally enabling schema V3 and shrinking existing key/phone columns. This follows the native .NET 10 Identity passkey entity/owned-JSON shape. Previous migrations are unchanged. The generated forward SQL is docs/identity-passkey-migration.sql. Down refuses to drop a nonempty passkey table to avoid deleting registered credentials.

The storage preparation does not enable passkey endpoints, change existing sign-in, create user records or register any keys. The user separately approved applying this migration, and it was applied on 2026-11-15 using the hash-verified guarded procedure in tests/Apply-IdentityPasskeyMigration.ps1. No seed, credential registration or SQL write test was run. Pre/post counts and content fingerprints matched across all 38 existing business/Identity tables. See identity-passkey-migration-verification.md. Do not rerun the applied migration.

## Safe verification

Solution and Razor build passed with zero warnings/errors. SchemaChecks verifies eight source migrations, model/snapshot agreement, unique credential primary key, restricted foreign key, and that the new forward migration contains only one CreateTable and one CreateIndex. Schema checks do not apply migrations. --write-passkey-script only writes the review SQL. --inspect-passkey-database issues one fixed SELECT for user/role/migration counts and passkey-table presence; it prints no user identifiers or credential data.

## Next implementation boundary

Storage is now applied. The next separately continued implementation is to implement native SignInManager ceremonies in MVC/Razor with vanilla browser WebAuthn, strict HTTPS/RP/origin checks, user verification required, protected short-lived ceremony state, antiforgery and current account eligibility. Require password reauthentication before enrollment/removal, scope credentials to the signed-in account, bound credentials/display names, preserve password fallback and test replay/origin/disabled-account/ownership failures. Do not invent raw biometric matching or an external SDK. No passkey UI or working biometric login is claimed by this storage-only increment.

Official references: https://learn.microsoft.com/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0 and the installed .NET 10.0.15 Identity APIs.
