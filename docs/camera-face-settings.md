# Camera face settings and shared appearance

## Approved scope

The user selected Orange's workspace composition with STMS's existing navy, ivory and gold identity, requested light/dark appearance and portable voice settings, and approved a **camera face template for verification inside an already signed-in account**. Device face/fingerprint/PIN sign-in remains the existing Identity WebAuthn implementation. A camera descriptor is never accepted as a login credential or authorization proof.

This is an independent MVC implementation, not a copy of Orange application source. Orange's face implementation explicitly permits photo/video spoofing; that limitation is not carried into STMS authentication. No liveness guarantee is claimed for camera template verification.

## Activation requires separate database approval

New migration: `20261116032101_CameraFaceSettings`.

It adds only `FaceCredentials`: Identity user ID primary key/foreign key (no cascade), an encrypted binary template (bounded to 8192 bytes in the model), created/updated UTC times and SQL rowversion. One record per user; no existing tables/records are rewritten. The rollback refuses to delete enrolled templates. The exact forward SQL is in `docs/camera-face-migration.sql`.

**The migration has not been applied.** Normal startup does not migrate or seed. `FaceRecognition:Enabled` defaults to false; `/FaceSettings` renders an awaiting-activation explanation without querying the new table. Only after separate approval, application of the reviewed migration and verification of persistent ASP.NET Core Data Protection key storage should deployment configuration enable it. Keep keys protected and persistent across app restarts; losing keys makes templates unreadable and requires re-enrollment.

## Template protection

- Identity eligibility and account lockout checked for every operation; users cannot select another user ID.
- Current password rechecked with Identity lockout for enrollment/replacement/removal.
- HTTPS, authenticated-user policy, antiforgery, request-size limit and per-IP rate limit.
- Account/security-stamp-bound, two-minute, atomically consumed capture tickets; invalid capture also consumes the ticket.
- Three to five bounded 128-value finite, nonzero, consistent descriptors. Five distinct camera frames collected by the UI.
- Binary encoding avoids unbounded JSON payloads in stored templates. ASP.NET Core Data Protection encrypts and authenticates the template with a per-user purpose. Password/security-stamp changes invalidate existing templates for verification.
- Rowversion checks reject stale replacement/removal; primary key prevents concurrent duplicate enrollment.
- Browser camera and microphone permissions are never copied or bypassed. Video/audio/images are not persisted or submitted. Camera tracks stop on success, error, cancellation, visibility changes and page exit, including late permission grants.
- Matching uses multiple descriptors with a conservative distance threshold; false matches/nonmatches remain possible. It has no effect on sign-in, roles, permissions or workflows. Multi-instance deployments need shared one-use challenge storage before enabling this feature across instances.

## Approved third-party assets

Only the standalone **face-api.js** browser distribution and three required model sets are included under `wwwroot/lib/face-api`, retaining the upstream MIT license. Source: https://github.com/vladmandic/face-api (browser library version 1.7.13). The pretrained detector/landmark/recognition model assets come from that distribution; no Orange C#/JavaScript business code is copied. Library and models are loaded **only after a deliberate camera action**, never globally. No package manager install or frontend framework is introduced.

## Appearance, voice and performance

- `appearance-init.js` applies local/system appearance before first paint. `appearance.js` handles toggles, cross-tab/system updates and same-origin popup appearance. If localStorage is unavailable, the page still works.
- Existing public links, role-aware sidebar, native forms, validation and authorized search action remain intact. Desktop header offers a direct text search; the voice icon stays beside notifications on every workspace page. English/Arabic selection is remembered locally. Speech still requires explicit activation and explicit search submission; browser permission/provider limitations remain visible.
- Only public mapped wwwroot assets short-circuit the authentication pipeline. Private documents remain controller-served with authorization. See Microsoft's guidance: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0#short-circuit-the-middleware-pipeline
- Notification list/count no longer block each page render. Existing authorized inbox/SSE endpoints populate the badge after paint; unknown is not represented as zero. The full notifications route works without JavaScript.
- Shorter transform/opacity entrances replace blur effects; table rows no longer each receive an animation observer. Reduced-motion settings remain respected.

Database-free checks cover template validation/encoding/encryption, replay, ownership, password reauthentication, stale forms, MVC antiforgery, disabled-feature behavior, public-asset middleware bypass, protected routes, theme persistence, seven role workspaces, mobile drawer and responsive tables. Physical camera recognition and production SQL persistence are separate acceptance checks and have not been performed by these fixtures.

## Verification results — 2026-11-16

| Check | Result |
|---|---|
| Solution build, `--no-restore` | Passed; 0 warnings, 0 errors |
| Compiled MVC face settings and domain security | 43 database-free checks passed |
| Camera interface/lifecycle and real local library/model loading | 15 checks passed; camera lifecycle simulated, blank-frame inference used real models |
| Appearance, persistence, seven roles, login and responsive layouts | 74 checks passed |
| Existing page sweep | 34 pages / 152 presentation checks passed; no JavaScript errors |
| Existing voice browser regression | 188 checks passed; speech events simulated |
| Existing voice MVC regression | 36 checks passed |
| Existing WebAuthn/password regression | 16 checks passed |
| Notification/message isolation, read actions and deferred menu | 22 database-free checks passed |
| Public static assets, ETags and protected routes | 16 checks passed |
| EF design model / migration schema verification | Passed offline; 9 migration definitions, no SQL operations |

Screenshots/logs remain in ignored local `App_Data` QA folders. No SQL write tests or migrations were executed. Automatic review rejected the command to launch the production-configured STMS process in the background without a detailed reason, so no alternative launch was attempted. Orange's isolated running preview was left untouched.
