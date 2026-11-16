# Public website composition — 2026-11-16

The user clarified that the requested Orange-style UI concerns the pages **before sign-in**. This change is restricted to public Razor presentation and frontend assets; internal dashboards, controllers, Identity, permissions, database configuration and migrations are unchanged.

## Reference and identity

Inspected the running isolated Orange homepage and actual public Home/About/Contact, shared header/footer and Identity login views. Independently recreated its full-width header, left-aligned architectural hero, flowing six-stage illuminated journey, capability strip, editorial About composition, split Contact layout and two-column account layouts using STMS's existing navy/ivory/gold palette and supplied logo.

The architectural illustration is an original, lightweight local SVG, not Orange's building photograph or a claim about STMS offices. Orange application source, logos, private/licensed assets, fictional metrics, unfinished explorers, Team pages and nonworking video buttons are not copied. Existing STMS footer links and the previously approved Home sticky-footer behavior remain intact with updated styling.

Read Section 5 of the original V5 DOCX: the exact required hero headline, six required navigation destinations, six workflow stages, database-backed statistics and About/Contact content remain. Existing supplier registration fields, uploads, validation, antiforgery and the real contact action are preserved.

## Files

- Home/Index: new architectural showcase, real interactive workflow summary, capability strip and preserved statistic bindings.
- Home/About: editorial heading/panorama and existing mission/lifecycle/principles content.
- Home/Contact: editorial copy/layout; existing real message form unchanged.
- Suppliers/Register: public onboarding introduction alongside the original form.
- Shared/_Layout: new CSS/JavaScript loaded only for the public shell.
- Shared/_PublicNavigation: appearance control outside the mobile menu.
- Shared/_PublicJourney: six accessible stage buttons and real play/pause/restart controls.
- `public-reference.css`, `public-reference.js`, `sourcing-architecture.svg`: scoped, local frontend implementation.
- Browser tests: new public reference checks and an updated public appearance assertion.

Login receives its public layout styling through CSS while preserving its existing Identity-bound forms and passkey implementation. No login logic changes are made.

## Behavior and verification

The journey advances the active stage and animates the path, supports explicit selection and keyboard arrows/Home/End, pauses on focus/out-of-view/hidden documents, and respects reduced motion. Reveals use transform/opacity. No scroll interception, form interception, framework or library is added.

- Solution build: passed, 0 warnings and 0 errors.
- Public composition: 335 memory-only checks across five routes, five viewport widths (1440, 1024, 768, 390, 320), both themes, animation controls, headline, metrics semantics and internal-shell isolation.
- Existing public shell regression: 264 checks passed, including navigation consistency, mobile keyboard behavior, existing supplier fields, password reveal and real MVC contact submission against an in-memory store.
- Additional keyboard check: passed; arrow navigation follows the focused stage even before it is selected.
- No JavaScript runtime errors observed; script syntax check passed.
- Screenshots reviewed for desktop/mobile and light/dark; artifacts remain under ignored local `App_Data/public-reference-qa` and `App_Data/public-reference-regression`.
- No SQL commands, migrations, database writes, dependency installations or production form submissions performed.
