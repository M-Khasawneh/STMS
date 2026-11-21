# Optional camera sign-in

Users first sign in with their password or an existing passkey. In **Camera face settings**, they register a face and separately choose **Enable camera sign-in**, confirming their current password. Registration alone never enables authentication. They can disable the option or remove their template; replacement also resets the option to disabled.

On the login page, **Sign in with camera** uses the username entered above and the browser's front camera. It requires HTTPS, camera permission and a compatible browser. This is independent of device Face ID, Windows Hello and WebAuthn passkeys.

## Storage and compatibility

No schema change or migration is required. The opt-in is part of the existing encrypted `FaceCredentials.ProtectedTemplate` payload. Binary format 2 appends the boolean; format 1 remains readable and always defaults to disabled. Encryption purpose and per-user protection scope are unchanged. Password/security-stamp changes invalidate the previous template. Users must register again after such changes.

Older application versions that only read format 1 cannot verify format 2 templates. Consider this when rolling back; never enable older templates implicitly or remove encryption to restore access. Password and passkey sign-in remain available.

## Enforcement

Settings changes require an authenticated eligible account, CSRF protection, current-password confirmation and the existing row version. Anonymous login uses a short-lived, single-use server challenge bound to a Secure/HttpOnly/SameSite cookie. Disabled, absent, changed or incompatible templates cannot authenticate. Username lookup does not return an enrollment/eligibility flag or any template to the client.

Matching uses the existing multiple-sample threshold. Login checks account eligibility and lockout, counts failed matches toward Identity lockout, and uses Identity's sign-in/two-factor path. Camera login does not bypass two-factor requirements; if that path cannot complete, the user must use an existing sign-in method. Settings writes and camera sign-in are serialized within the existing single-process challenge lifetime; row versions also protect stale settings updates.

Camera measurements are produced by client-side JavaScript. Photos, videos or manipulated/replayed descriptors may defeat this mechanism; the one-use challenge does **not** turn a face descriptor into a cryptographic authenticator or prove liveness. This convenience option is weaker than a verified passkey. Images/video are not uploaded or saved by this implementation, and descriptors must not be logged.

## Verification

Database-free HTTPS MVC checks cover opt-in, legacy data, cookie binding, CSRF, mismatch, opt-out during capture, missing templates, security stamps, eligibility, lockout and two-factor refusal. Browser checks use synthetic camera frames and verify registration, opt-in, passwordless camera login, opt-out, cancellation and permission denial. These are not substitutes for a real-device enrollment/login test after deployment.
