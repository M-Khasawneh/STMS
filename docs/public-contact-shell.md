# Public contact and shared public shell

User-authorized scope: add a real Contact form addressed to Admin, improve the public footer and navigation (especially Login), make login calmer and modern, and remove the navbar difference between Home and Login. No procurement workflows, Identity configuration, entities, roles or migrations changed.

## Contact delivery

- `/Home/Contact` GET and POST use normal MVC/Razor, server/client validation, antiforgery and no-store responses. Fields: sender name (100 characters), email (254), subject (180), message (4000). The 64 KiB request cap accommodates URL-encoded Unicode text within those field limits.
- The application validates and trims the message, then queries current Identity Admin memberships. Locked accounts and Supplier-role/profile accounts are excluded. Admin's system-wide recipient eligibility does not depend on a department.
- Each eligible Admin receives a private unread Notification with the visitor's name, email, subject and message. Existing recipient-scoped Notifications pages and unread bell display it. Plain text is Razor-encoded and line breaks are preserved. No public record lookup, email transport, attachments, reply/ticket workflow or fabricated contact details were added.
- All recipient notifications save together through EF's transaction. Confirmation appears only after successful persistence, using post/redirect/get. Missing eligible admins or storage failures return a recoverable failure, never a fake success.
- Built-in ASP.NET rate limiting allows three Contact POST requests per minute per direct client IP, with HTTP 429/retry guidance. No forwarded-IP trust or proxy/auth configuration was changed. This is per application instance; a distributed anti-spam system is outside scope.

## Shared presentation

- `_PublicNavigation.cshtml`, `_PublicFooter.cshtml` and `public-shell.css` are shared by public pages, Login and Supplier Registration. The supplied logo appears consistently in the shared navbar; the login form still has no decorative logo or side panel.
- Floating navigation, current destinations, visible navy Login button with an icon, supplier registration action, keyboard/mobile menu, Escape/focus restoration and reduced-motion support are retained consistently. The existing `home.js` is loaded once by the public layout instead of separate pages.
- Footer uses the approved navy/gold identity, real platform/account links, a Contact action and responsive columns. Privacy navigation remains restricted to Home as previously requested. Internal sidebar/footer behavior is preserved.
- Login is centered, with restrained surfaces, clearer spacing and an accessible show/hide password control. Authentication bindings, return URLs, cookies, authorization and validation are unchanged.
- Free 21st metadata references consulted: [Navbar](https://21st.dev/@AliFarooqDev/components/navbar), [Login](https://21st.dev/@ephraimduncan/components/login-1). Implemented independently in Razor/CSS/vanilla JavaScript; no paid code retrieval, packages or frontend frameworks. The 21st CLI is unavailable, so its CLI init/review commands were not run.

## Verification

- Build passed with zero warnings/errors.
- 19 database-free Contact service/query/MVC checks passed: input validation, normalized delivery, actual role-query translation, form binding, PRG, escaped redisplay, storage failures, no-recipient handling, rate limiting and antiforgery metadata.
- After separate user approval, nine SQL checks passed using the real Contact service/store and existing notification inbox query. The fixture verified recipient exclusion, persisted sender/body, unread UTC metadata, supplier isolation and failed-save behavior. All writes stayed in an explicit transaction and were rolled back; fingerprints confirmed original users, role assignments and notifications were unchanged. No test enquiry was committed to a live Admin inbox.
- 229 Edge browser checks passed across 1440/1024/768/390/320 px: identical computed navbar presentation on five public routes, footer/navigation links, mobile Login visibility/contrast, keyboard behavior, password reveal/conceal, successful memory-only Contact POST/confirmation and reduced motion. No JavaScript errors. Screenshots remain outside the repository.
- Existing database-free authentication/Employee/Department suite and 45 workspace routing/role checks passed. No production users, database schema or migrations changed.
