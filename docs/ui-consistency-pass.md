# UI consistency pass — 2026-11-16

Scope: buttons, spacing, cards, dark appearance and the shared workspace search panel. No production controllers, services, models, permissions, business workflows, database configuration or migrations changed.

## Changes

- Shared spacing tokens: page inset 32px desktop / 24px tablet / 16px mobile; section gap 24px; card inset 24px desktop / 20px mobile; normal control gap 12px. Metric cards and compact controls retain intentional smaller insets.
- Shared 44px form/action buttons, 10px control radius and 16px card radius. Primary, secondary, destructive, focus and disabled presentations preserve existing action semantics.
- A quieter navy/slate dark palette with separate canvas, panel and raised surfaces. Primary button foregrounds/backgrounds, muted text, status tones, table striping, dropdown arrows and mobile header buttons now adapt to the selected theme.
- Corrected light backgrounds inside requisition specifications, progress branches, supplier registration fieldsets, report rows and the Admin activity table.
- Aligned role dashboard headings, card padding, record lists, metrics, forms and table cells. Admin distribution counts, chart colors, activity links and auxiliary labels remain readable in dark mode.
- Rebuilt the search panel as a compact 480px non-modal surface, with responsive mobile placement, a clear query field, balanced controls and a single focus treatment. Short introductory copy replaces a dense explanation; detailed voice guidance is expandable, while the possibility of online audio processing remains visible before activation.
- Search still uses the existing native MVC GET action, ownership-aware backend and existing voice JavaScript. Clear, close, keyboard focus, Escape, language selection, cancellation and explicit submission retain their existing behavior.
- Public dark feature cards, About/Contact text, supplier registration, login callouts and action buttons use the same palette. Existing public content, routes and statistics bindings are retained.

## Verification

All browser checks use explicitly marked in-memory MVC fixtures with synthetic records. The new visual audit blocks POST requests. No SQL commands, migrations, real-account sign-ins or production form submissions were performed.

| Check | Result |
|---|---|
| Solution build, `--no-restore` | Passed: 0 warnings, 0 errors |
| Shared UI audit | 395 checks passed; seven roles, both themes, desktop/tablet/mobile, populated Admin overview and representative internal/public pages |
| Solid-surface text contrast / bright-panel audit | No remaining findings in the sampled rendered states; no JavaScript errors |
| Existing page sweep | 34 pages / 152 checks passed |
| Existing voice/search browser regression | 188 checks passed; speech events simulated |
| Existing public presentation regression | 335 checks passed |
| Existing appearance regression | 74 checks passed |
| Compiled search MVC audience/no-store regression | 36 checks passed |
| Final compiled search notice and screenshots | Passed; voice privacy summary remains visible before activation |

The contrast audit measures rendered text against composited solid backgrounds; gradient/image backgrounds and disabled controls are excluded from that numerical check and reviewed visually. This is not a certification of every possible content/state or an end-to-end production SQL test. Physical microphone/camera behavior was not exercised by this UI pass.

Review used actual compiled Razor screenshots and browser measurements. The 21st command-palette catalog was consulted as interaction context; no component code, frontend framework or dependency was installed. The 21st CLI is unavailable locally, so no CLI review result is claimed.

Files: `Views/Shared/_WorkspaceSearch.cshtml`; `wwwroot/css/appearance.css`, `dashboard-search.css`, `public-reference.css`, `workspace-refinements.css`; new `tests/ui-consistency-browser.cjs` and test-only fixture flag. Before/after/final screenshots and JSON results remain under ignored local `App_Data/ui-consistency-*` folders.
