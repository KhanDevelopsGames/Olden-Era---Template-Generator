# Web E2E Tests — Minimal Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a tiny Playwright smoke suite for `OldenEra.Web` that catches "the app crashed" regressions without locking in current UI structure.

**Architecture:** Playwright (Node) lives in `tests/e2e/` at repo root. `playwright.config.ts` boots `dotnet run --project OldenEra.Web` as `webServer`, points at `http://localhost:5230`. Chromium-only, headless. Tests assert behavior (Generate produces a preview, downloads fire) — not DOM shape — so they survive the planned UI churn.

**Tech Stack:** Playwright `@playwright/test`, Node 20, Chromium. CI on `ubuntu-latest` (the web project is `net10.0`, no Windows runtime needed). Reuses `wasm-tools` workload install pattern from `tests.yml`.

**Scope guard:** ~5 tests. If you find yourself asserting on `.oe-nav-item` count, panel layout, button labels beyond "Generate" — stop. The plan is "Generate works, downloads work, basic tab navigation doesn't crash." Nothing more.

---

## Pre-flight: layout & selector audit

Before writing tests, confirm the assumptions below match `Pages/Home.razor` and `Components/`:

- **Generate button:** `button.oe-btn.primary` with text "Generate Template". Disabled when invalid or generating.
- **Preview img:** `.oe-preview img` appears once `PreviewPng` is set. Source is `data:image/png;base64,…`.
- **Download buttons:** `button.oe-btn` with text `Download map` and `Download map w/ installer`. Disabled until generation completes.
- **Tabs:** `nav.oe-nav-list button.oe-nav-item` (5 items). Plus a fallback `select.oe-nav-select` that mobile shows. Don't test the select — the `<nav>` buttons are always in the DOM.
- **Default settings produce a small valid map.** `GeneratorSettings` defaults are valid (template name "Custom Template", default size, 4 players, no neutral zones). Generation on defaults should complete in seconds in WASM.
- **Large-map confirmation modal** triggers at `MapSize >= 240` or total zones `>= 16`. Defaults are well below; tests don't need to dismiss it. If a test ever hits it, fix the test (use smaller settings) — don't add modal handling.

If any selector above no longer matches, fix the test selector — don't add `data-testid` unless the existing class is genuinely ambiguous.

---

## Task 1: Scaffold Playwright

**Files:**
- Create: `tests/e2e/package.json`
- Create: `tests/e2e/playwright.config.ts`
- Create: `tests/e2e/.gitignore`
- Modify: `.gitignore` (add `tests/e2e/node_modules/`, `tests/e2e/test-results/`, `tests/e2e/playwright-report/`)

**Step 1: Create `tests/e2e/package.json`**

```json
{
  "name": "olden-era-e2e",
  "private": true,
  "version": "0.0.0",
  "scripts": {
    "test": "playwright test",
    "test:headed": "playwright test --headed",
    "install-browsers": "playwright install --with-deps chromium"
  },
  "devDependencies": {
    "@playwright/test": "^1.49.0",
    "typescript": "^5.6.0"
  }
}
```

**Step 2: Create `tests/e2e/playwright.config.ts`**

```ts
import { defineConfig, devices } from '@playwright/test';

const PORT = 5230;
const BASE_URL = `http://localhost:${PORT}`;

export default defineConfig({
  testDir: './specs',
  // Generation in WASM on first hit can take a while; give the test room.
  timeout: 120_000,
  expect: { timeout: 30_000 },
  fullyParallel: false, // single web server, single browser tab keeps things simple
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : 'list',
  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    // -c Release so the WASM bundle is the size users actually run; also avoids
    // dev-mode hot-reload noise.
    command: 'dotnet run --project ../../OldenEra.Web -c Release --no-launch-profile --urls http://localhost:5230',
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 300_000, // first-time WASM build + workload restore is slow
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
```

**Step 3: Create `tests/e2e/.gitignore`**

```
node_modules/
test-results/
playwright-report/
playwright/.cache/
```

**Step 4: Append to repo root `.gitignore`**

Run: `grep -q "tests/e2e/node_modules" .gitignore || printf "\n# Playwright e2e\ntests/e2e/node_modules/\ntests/e2e/test-results/\ntests/e2e/playwright-report/\n" >> .gitignore`

**Step 5: Install + verify Playwright config parses**

```bash
cd tests/e2e
npm install
npx playwright install --with-deps chromium
npx playwright test --list
```

Expected: lists 0 tests (no specs yet) without config errors.

**Step 6: Commit**

```bash
git add tests/e2e/ .gitignore
git commit -m "test(e2e): scaffold Playwright with webServer pointing at OldenEra.Web"
```

---

## Task 2: First smoke test — page loads

**Files:**
- Create: `tests/e2e/specs/smoke.spec.ts`

**Step 1: Write the test**

```ts
import { test, expect } from '@playwright/test';

test('home page loads with header and Generate button', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/Olden Era/i);
  await expect(page.getByRole('button', { name: /Generate Template/i })).toBeVisible();
});
```

**Step 2: Run it**

```bash
cd tests/e2e && npm test -- --grep "home page loads"
```

Expected: PASS. The first run boots `dotnet run` for `OldenEra.Web`; this can take a couple of minutes the first time. Subsequent runs reuse the dev server locally (CI starts fresh).

**Step 3: If it fails**

- Check `dotnet run --project OldenEra.Web -c Release` works in your shell (workload, port 5230 free).
- Check the heading text in `Pages/Home.razor` (currently "Olden Era — Simple Template Generator").
- Don't loosen the assertion past `/Olden Era/i` — that's already loose enough to survive UI tweaks.

**Step 4: Commit**

```bash
git add tests/e2e/specs/smoke.spec.ts
git commit -m "test(e2e): assert home page renders header and Generate button"
```

---

## Task 3: Generate produces a preview image

**Files:**
- Modify: `tests/e2e/specs/smoke.spec.ts`

**Step 1: Add the test**

```ts
test('Generate produces a preview image', async ({ page }) => {
  await page.goto('/');

  const generate = page.getByRole('button', { name: /Generate Template/i });
  await expect(generate).toBeEnabled();
  await generate.click();

  // Spinner may appear briefly. Then a PNG preview <img> with a data: URL.
  const preview = page.locator('.oe-preview img');
  await expect(preview).toBeVisible({ timeout: 60_000 });

  const src = await preview.getAttribute('src');
  expect(src).toMatch(/^data:image\/png;base64,[A-Za-z0-9+/=]+$/);
  // Sanity: payload is non-trivial (more than just a few bytes of base64).
  expect((src ?? '').length).toBeGreaterThan(1000);
});
```

**Step 2: Run it**

```bash
cd tests/e2e && npm test -- --grep "Generate produces"
```

Expected: PASS. First WASM generate is the slowest step in the suite — that's why timeout is 60 s.

**Step 3: If it hangs or hits the large-map modal**

- Defaults should be a small map. If they aren't, the test should explicitly set `MapSize` to e.g. 96 by interacting with the slider — but only then. Don't preemptively add slider interaction.
- Check the browser console via `page.on('pageerror', ...)` if it crashes — Blazor's red banner hides the real error.

**Step 4: Commit**

```bash
git add tests/e2e/specs/smoke.spec.ts
git commit -m "test(e2e): assert Generate yields a non-trivial PNG preview"
```

---

## Task 4: Download buttons fire downloads

**Files:**
- Modify: `tests/e2e/specs/smoke.spec.ts`

**Why test both:** Download map (plain zip via `BuildPlainZip`) and Download map w/ installer (`BuildInstallerZip`) are separate code paths in `InstallerPackager`. A previous regression broke one without the other.

**Step 1: Extract a helper**

At the top of `smoke.spec.ts`, add:

```ts
async function generate(page: import('@playwright/test').Page) {
  await page.goto('/');
  await page.getByRole('button', { name: /Generate Template/i }).click();
  await expect(page.locator('.oe-preview img')).toBeVisible({ timeout: 60_000 });
}
```

Refactor Task 3's test to use this helper to avoid duplication.

**Step 2: Add the download test**

```ts
test('Download map produces a .zip', async ({ page }) => {
  await generate(page);

  const downloadButton = page.getByRole('button', { name: /^Download map$/i });
  await expect(downloadButton).toBeEnabled();

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    downloadButton.click(),
  ]);

  expect(download.suggestedFilename()).toMatch(/\.zip$/);
});

test('Download map w/ installer produces a .zip', async ({ page }) => {
  await generate(page);

  const installerButton = page.getByRole('button', { name: /Download map w\/ installer/i });
  await expect(installerButton).toBeEnabled();

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    installerButton.click(),
  ]);

  expect(download.suggestedFilename()).toMatch(/-installer\.zip$/);
});
```

**Step 3: Run the suite**

```bash
cd tests/e2e && npm test
```

Expected: 4 PASS.

**Step 4: If a download never fires**

- The web project uses `FileDownloader` JS interop. In Chromium under Playwright this works without extra config, but `acceptDownloads` defaults to true only on contexts created from `test`. Confirm `page.context().on('download', ...)` doesn't show the download being suppressed.
- Don't assert on file *contents* — that's the renderer's job, covered by xUnit. We only care that the click → download path fires.

**Step 5: Commit**

```bash
git add tests/e2e/specs/smoke.spec.ts
git commit -m "test(e2e): assert Download map and installer buttons fire .zip downloads"
```

---

## Task 5: Tabs are clickable (cheap, breaks loudly if nav rewrites crash)

**Files:**
- Modify: `tests/e2e/specs/smoke.spec.ts`

**Step 1: Add the test**

```ts
test('each top nav item is clickable without crashing', async ({ page }) => {
  // Capture any uncaught Blazor errors so the test fails loudly instead of
  // showing the generic red banner.
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));

  await page.goto('/');

  const navItems = page.locator('nav.oe-nav-list button.oe-nav-item');
  const count = await navItems.count();
  expect(count).toBeGreaterThanOrEqual(3); // sanity: a few tabs exist

  for (let i = 0; i < count; i++) {
    await navItems.nth(i).click();
    // The active class is the one structural assertion we keep — if this
    // changes the rest of the test should be reviewed too.
    await expect(navItems.nth(i)).toHaveClass(/active/);
  }

  expect(errors).toEqual([]);
});
```

**Why no per-panel assertions:** the user said the UI is going to change. Clicking each tab and asserting "no JS error + the clicked item became active" is the most we should commit to right now.

**Step 2: Run it**

```bash
cd tests/e2e && npm test -- --grep "nav item is clickable"
```

Expected: PASS.

**Step 3: Commit**

```bash
git add tests/e2e/specs/smoke.spec.ts
git commit -m "test(e2e): assert every nav tab activates without throwing"
```

---

## Task 6: CI workflow

**Files:**
- Create: `.github/workflows/e2e.yml`

**Step 1: Write the workflow**

```yaml
name: Olden Era Web E2E

on:
  push:
    branches: ["**"]
  pull_request:
    branches: ["**"]

jobs:
  e2e:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install wasm-tools workload
        run: dotnet workload install wasm-tools

      - name: Restore NuGet
        run: dotnet restore OldenEra.Web/OldenEra.Web.csproj

      - name: Build OldenEra.Web (Release)
        run: dotnet build OldenEra.Web/OldenEra.Web.csproj -c Release --no-restore

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
          cache-dependency-path: tests/e2e/package-lock.json

      - name: Install e2e deps
        working-directory: tests/e2e
        run: npm ci

      - name: Install Playwright browsers
        working-directory: tests/e2e
        run: npx playwright install --with-deps chromium

      - name: Run e2e
        working-directory: tests/e2e
        run: npm test
        env:
          CI: 'true'

      - name: Upload Playwright report on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: tests/e2e/playwright-report
          retention-days: 7
```

**Why ubuntu-latest:** `OldenEra.Web` is `net10.0`. Existing `tests.yml` uses `windows-latest` only because the *test project* targets `net10.0-windows`. The web app has no Windows-specific code — Linux is faster and cheaper.

**Why a separate workflow file:** keeps the existing `tests.yml` untouched; the E2E job has very different deps (Node, browsers) and timing. Splitting also means a flaky e2e doesn't block xUnit.

**Step 2: Commit `package-lock.json` if `npm install` produced one**

```bash
cd tests/e2e && npm install # generates package-lock.json if missing
git add tests/e2e/package-lock.json .github/workflows/e2e.yml
git commit -m "ci: run web e2e suite on ubuntu via Playwright"
```

**Step 3: Push and watch the workflow**

Push the branch and confirm the new workflow runs and goes green. If it fails:
- Cold WASM build is slow — the 300 s `webServer.timeout` should cover it, but if CI consistently exceeds it, raise to 480 s rather than chasing other causes first.
- If `npx playwright install` fails on missing system libs, `--with-deps` should fix it — this is the entire reason for that flag.

---

## Task 7: README pointer

**Files:**
- Modify: `README.md` (or `AGENTS.md` if the project prefers that for tooling notes)

**Step 1:** Add a short section near the existing test instructions:

```markdown
### End-to-end (web) tests

Playwright smoke tests live in `tests/e2e/`. They boot `OldenEra.Web` and
exercise Generate + the download buttons. Run locally:

```bash
cd tests/e2e
npm install
npx playwright install --with-deps chromium
npm test
```

CI runs them on Ubuntu via `.github/workflows/e2e.yml`.
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: point at the new tests/e2e Playwright suite"
```

---

## Done criteria

- 5 specs in `tests/e2e/specs/smoke.spec.ts`, all green locally and in CI.
- Cold local run from `npm test` boots the web app and finishes inside the configured timeout.
- A deliberately broken Generate button (e.g. throw inside `RunGenerationAsync`) makes the suite fail with a useful error from the `pageerror` listener — verify this manually once before considering the suite done. Revert the sabotage.
- The plan stub `docs/plans/2026-05-10-web-e2e-tests.md` can be left in place; it tracked the "should we do this" question. Optionally update its status line to "implemented in <commit hash>" once merged.

## What this plan deliberately doesn't do

- No `.oetgs` save/open round-trip test. UI for that is in flux.
- No share-link clipboard test. Clipboard in headless Chromium needs permissions ceremony, payoff is small.
- No validation/error-banner tests. Validation messages will likely be reorganized in the next UI pass.
- No responsive/viewport tests. Defer until the responsive layout is locked in.
- No visual diffing.

When the UI settles, revisit `docs/plans/2026-05-10-web-e2e-tests.md` for the broader 10-test list.
