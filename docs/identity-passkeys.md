# Identity passkeys — storage applied, sign-in not yet enabled

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
