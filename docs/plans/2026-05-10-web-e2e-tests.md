# Web E2E Tests — Plan Stub

**Status:** not started. Captured 2026-05-10 as a follow-up to the UI rework.

## Why

The repo has zero UI test surface. xUnit covers the generator and renderer only. During the UI rework the user surfaced two regressions a smoke test would have caught immediately:

- Advanced lived in its own nav tab, separating it from the toggle that controlled it.
- Toggling Advanced hid the simple "Additional Neutral Zones" slider entirely (read as "an option disappeared").

Both shipped because we had no way to exercise the click-flow automatically. A handful of Playwright smoke tests would catch the next equivalent regression.

## Scope (smallest useful)

5–10 tests. No visual diffing. No deep coverage. Just "the UI loads and the golden path doesn't crash."

Suggested cases:

1. Page loads, header renders, version badge present.
2. Each of the 5 nav items is clickable; clicking it shows the corresponding panel and hides the others.
3. Toggling Advanced in Zones reveals the per-tier sliders and disables (does not hide) the simple slider.
4. Each of the 4 topology tiles is selectable; selected tile gets the amber border.
5. Generate on a small map (e.g. 96x96, 4 players, 2 neutral zones) produces a non-zero `<img>` src within ~10 s.
6. Download `.rmg.json` triggers a download with the right filename.
7. Save settings → Open the resulting `.oetgs` → settings restore identically.
8. Share-link button copies to clipboard and shows the toast.
9. Validation: clearing template name surfaces a red error under Generate; Generate is disabled.
10. Resize to 800px and 500px — nav adapts (pill strip, then `<select>`).

## Tooling

- **Playwright** (Node, single binary). Lighter than Selenium; supports headed/headless and works against any localhost.
- Run against `dotnet run --project OldenEra.Web` started in `webServer` mode in `playwright.config.ts`.
- One `tests/e2e/` folder at repo root or under `OldenEra.Web/tests/e2e/`.
- CI: new `e2e.yml` workflow on `ubuntu-latest` (web project is `net10.0`, not Windows-locked). Install `wasm-tools` workload like `tests.yml` does.

## Selectors

The Razor markup uses semantic-ish classes (`.oe-nav-item`, `.oe-topo-tile`, `.oe-btn.primary`). Add `data-testid` only where the existing class is genuinely ambiguous — don't sprinkle them everywhere.

## Don't bother with

- Pixel diffing / visual regression — too noisy, too maintenance-heavy.
- WPF — Playwright doesn't help there; Windows host runtime needed.
- Cross-browser matrix — Chromium-only is enough for a tool this niche.
- Mocking the generator — actually run it; the WASM bundle is small.

## Sequencing

Wait until the UI has settled. As of this PR (modern dark restyle + stepper layout) it's stable; if the next iteration changes panel structure significantly, hold off until that lands.

## Effort

Roughly half a day to scaffold + write the 10 tests, plus an hour to wire CI.
