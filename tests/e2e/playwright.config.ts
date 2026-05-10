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
