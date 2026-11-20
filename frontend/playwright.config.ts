import { randomBytes } from 'node:crypto'
import { defineConfig, devices } from '@playwright/test'

// End-to-end tests (E01-16, docs/TESTING.md): every test runs in both profiles, desktop 1440 px and mobile 390 px,
// against the production build (`pnpm build`, then `node .output/server/index.mjs`). Chromium only.
// Reports: JUnit XML for the verify summary; on a failure the trace and a screenshot stay in
// artifacts/test-results/frontend-e2e/failures (uploaded by CI as the test-results artifact).
const port = 3100
const providerPort = 3101
const apiPort = 3102
const reports = '../artifacts/test-results/frontend-e2e'

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
  // The site in its production build, signing in at the fake OpenID Connect provider (e2e/fake-oidc-provider.mjs) and
  // talking to the fake API (e2e/fake-api.mjs: hub tickets and the realtime hub): external services only as fakes
  // (docs/TESTING.md). The session key is new on every run, never committed.
  webServer: [
    {
      command: 'node e2e/fake-api.mjs',
      url: `http://127.0.0.1:${apiPort}/`,
      env: { FAKE_API_PORT: String(apiPort) },
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
    {
      command: 'node e2e/fake-oidc-provider.mjs',
      url: `http://127.0.0.1:${providerPort}/`,
      env: {
        FAKE_OIDC_PORT: String(providerPort),
        FAKE_OIDC_REDIRECT_URI: `http://127.0.0.1:${port}/auth/callback`,
      },
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
    {
      command: 'node .output/server/index.mjs',
      url: `http://127.0.0.1:${port}`,
      env: {
        PORT: String(port),
        HOST: '127.0.0.1',
        NITRO_PORT: String(port),
        NITRO_HOST: '127.0.0.1',
        NUXT_OIDC_ISSUER: `http://127.0.0.1:${providerPort}/`,
        NUXT_OIDC_REDIRECT_URI: `http://127.0.0.1:${port}/auth/callback`,
        NUXT_SESSION_PASSWORD: randomBytes(32).toString('base64url'),
        NUXT_API_BASE: `http://127.0.0.1:${apiPort}`,
        NUXT_PUBLIC_API_BASE: `http://127.0.0.1:${apiPort}`,
      },
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
})
