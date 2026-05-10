import { test, expect } from '@playwright/test';

async function generate(page: import('@playwright/test').Page) {
  await page.goto('/');
  await page.getByRole('button', { name: /Generate Template/i }).click();
  await expect(page.locator('.oe-preview img')).toBeVisible({ timeout: 60_000 });
}

test('home page loads with header and Generate button', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/Olden Era/i);
  await expect(page.getByRole('button', { name: /Generate Template/i })).toBeVisible();
});

test('Generate produces a preview image', async ({ page }) => {
  await generate(page);

  const preview = page.locator('.oe-preview img');
  const src = await preview.getAttribute('src');
  expect(src).toMatch(/^data:image\/png;base64,[A-Za-z0-9+/=]+$/);
  // Sanity: payload is non-trivial (more than just a few bytes of base64).
  expect((src ?? '').length).toBeGreaterThan(1000);
});

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

test('each top nav item is clickable without crashing', async ({ page }) => {
  // Capture any uncaught Blazor errors so the test fails loudly instead of
  // showing the generic red banner.
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));

  await page.goto('/');

  const navItems = page.locator('nav.oe-nav-list button.oe-nav-item');
  // Wait for Blazor to hydrate before counting; count() itself doesn't auto-wait.
  await expect(navItems.first()).toBeVisible();
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
