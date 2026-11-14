# efferd Sticky Footer reference

User-selected reference: https://21st.dev/@efferd/components/sticky-footer (21st demo 7242). Inspected the actual component/demo through the connected MCP using the remaining free retrieval. The reference uses a clipped outer container, viewport-fixed bottom surface, multi-column links, diagonal lighting and one-time blurred/staggered entrances (800 ms, -8 px, 4 px blur). Its demo adds Lenis, React and Motion.

Latest user adjustment: the sticky reveal is now enabled only on the public Home page. About, Contact, Login and Supplier Registration keep a normal-flow footer. The shared public footer uses a dark navy surface (#0b1826), light text and gold headings, retaining the same destinations and accessibility fallbacks.

STMS implements the same public interaction independently with Razor, native CSS, IntersectionObserver, ResizeObserver and Web Animations. The listing did not provide a component license, so no React/Motion source, demo copy or proprietary assets were embedded. Publicly observable layout/behavior informed the native implementation; no licensing terms are assumed.

- Replaces the previous ScrollX oversized-wordmark footer with branding, real platform/account links and the existing Contact destination. No social placeholders, fintech modules, invented contact details or unfinished destinations. Privacy remains Home-only.
- At desktop widths >=900 px and viewport heights >=650 px, an outer clip reveals a bottom-fixed surface as the document scrolls. Height is measured from real content, bounded by the usable viewport. Each visible group gets the reference's one-time entrance timing using native animation.
- Small/short screens, reduced motion, unsupported clip-path and JavaScript-disabled browsing keep ordinary document flow. There is no nested scrolling or scroll interception. Keyboard entry switches the surface into its flow position and brings the actual focused link into view.
- Scope: `_PublicFooter.cshtml`, footer-only rules in `public-shell.css`, new `sticky-footer.js` and one public-layout script include. No backend, schema, migration, roles or workflows changed. No packages or CDN dependencies installed.

Verification: solution build passed with zero warnings/errors; 49 targeted browser checks verified actual fixed-position geometry during scrolling, reachable links, keyboard flow, reduced motion, short/mobile layouts and no-JavaScript fallback across five public routes. Existing 229 public-shell browser checks passed. No JavaScript errors or SQL writes. Screenshots remain external QA artifacts.
