import { defineConfig, devices } from '@playwright/test'

// End-to-end tests of the admin panel (E01-16, docs/TESTING.md): every test runs in both profiles, desktop 1440 px
// and mobile 390 px, against the production build (`pnpm build`, then `vite preview`). Chromium only.
// Reports: JUnit XML for the verify summary; on a failure the trace and a screenshot stay in
// artifacts/test-results/admin-e2e/failures (uploaded by CI as the test-results artifact).
const port = 4174
const reports = '../artifacts/test-results/admin-e2e'

export default defineConfig({
  testDir: 'e2e',
  outputDir: `${reports}/failures`,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: [[process.env.CI ? 'line' : 'dot'], ['junit', { outputFile: `${reports}/junit.xml` }]],
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    locale: 'ru-RU',
    timezoneId: 'Europe/Moscow',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
    {
      name: 'mobile',
      // A 390×844 phone with touch (iPhone 13 user agent and pixel ratio), rendered by Chromium, the only browser installed.
      use: {
        ...devices['iPhone 13'],
        viewport: { width: 390, height: 844 },
        browserName: 'chromium',
        defaultBrowserType: 'chromium',
      },
    },
  ],
  webServer: {
    command: `pnpm exec vite preview --host 127.0.0.1 --port ${port} --strictPort`,
    url: `http://127.0.0.1:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
})
