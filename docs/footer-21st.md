# 21st.dev footer adaptation

Historical implementation: subsequently replaced at the user's request by [the efferd Sticky Footer interaction](footer-sticky-efferd.md). The retained ScrollX notice documents the previous version; the current native footer does not embed that component source.

Source: [Footer with Suite by ScrollX UI](https://21st.dev/@scrollxui/components/footer-with-suite), demo 29773. Actual component/demo source was retrieved through the connected 21st MCP using one free daily retrieval; no paid retrieval or generation was used.

The native Razor/CSS adaptation preserves its split navigation/description composition, oversized centered brand wordmark, bottom rule and link hover behavior. It uses STMS's ivory/navy/gold identity and real existing routes. The reference's demo social links, studio copy, fake email and placeholder destinations were replaced with existing platform/account/contact links. Privacy stays Home-only. No React, Tailwind runtime, dependencies, backend changes or database changes.

The 21st listing identifies the source as MIT. The author's repository license includes additional Commons Clause restrictions, explicitly allowing commercial applications, SaaS and internal business tools while restricting redistribution as component libraries/UI kits. This is an adaptation for this application, not a redistributed component package. Full source notice retained in [third-party/scrollx-ui-license.md](third-party/scrollx-ui-license.md), copyright 2025 Ahdeetai.

Changed production files: `_PublicFooter.cshtml` and the footer-only rules in `public-shell.css`. Build passed with zero warnings/errors. Public-shell browser regression checks cover responsive overflow, navigation, mobile menu, reduced motion and unchanged Contact/login behavior; screenshots are external QA artifacts.
