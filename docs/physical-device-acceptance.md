# Physical-device acceptance checklist

These steps require the account owner at the device. Do not send a password, PIN, face image, fingerprint sample or audio recording to the developer/chat. Run only at the intended HTTPS origin with a consenting test account and record pass/fail observations without biometric data.

## Face, fingerprint and PIN

1. Verify the device/OS already supports and has configured the intended native authenticator. Hardware may differ between users. Do not enable a camera, start services or install drivers automatically to fabricate support.
2. Sign in normally, open Profile / Passkeys, reauthenticate with the account's current password and explicitly register a clearly named test credential using the native prompt.
3. Log out and use the real passkey button. Demonstrate face verification on compatible face hardware and fingerprint verification on compatible fingerprint hardware. If separate devices/credentials are needed, test them separately. A PIN success alone does not demonstrate both biometric methods.
4. Separately demonstrate the approved PIN fallback, user cancellation and ordinary password fallback. STMS validates native user verification but cannot determine which biometric/PIN modality the OS selected.
5. Confirm account/role eligibility, correct HTTPS origin and that another account cannot manage this credential. Remove only the named test credential through the existing password-confirmed interface; check that it no longer authenticates.
6. Keep raw biometrics out of STMS and acceptance artifacts. Record browser/OS/authenticator class, observed method, outcome and the actual user-confirmed test date only.

## Real speech recognition

1. Sign in as an eligible employee and open the dashboard. Review the existing microphone/service notice before activating recognition; the browser speech service may process audio remotely.
2. Grant microphone permission explicitly. Speak a harmless English phrase and an Arabic phrase matching an authorized displayed work record. Confirm final transcript filtering and the existing authorized results.
3. Test stop/cancel, denied permission, service/network failure and unsupported-browser typed fallback. Confirm unrelated or other users' records are not revealed.
4. Do not confuse the injected-recognizer browser tests with this check. Do not save recordings or private transcripts as evidence.

No physical success is prefilled. App-protocol tests, actual device demonstrations and SQL persistence checks remain distinct evidence.
