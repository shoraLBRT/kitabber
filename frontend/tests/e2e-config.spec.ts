// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import config from '../playwright.config'

// The e2e setup of E01-16 (docs/TESTING.md): two profiles, desktop 1440 px and mobile 390 px, every test in both,
// Chromium only; the trace and a screenshot are kept on a failure; JUnit reports go to the verify summary.
const scripts = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8')).scripts

describe('Playwright config', () => {
  const projects = config.projects ?? []

  it('has the desktop 1440 px and mobile 390 px profiles in Chromium', () => {
    expect(projects.map((p) => p.name)).toEqual(['desktop', 'mobile'])
    const [desktop, mobile] = projects.map((p) => p.use ?? {})
    expect(desktop.viewport).toEqual({ width: 1440, height: 900 })
    expect(desktop.hasTouch ?? false).toBe(false)
    expect(mobile.viewport).toEqual({ width: 390, height: 844 })
    expect(mobile.hasTouch).toBe(true)
    expect(mobile.isMobile).toBe(true)
    for (const use of [desktop, mobile]) expect(use.browserName ?? 'chromium').toBe('chromium')
  })

  it('runs every test in both profiles', () => {
    for (const project of projects) {
      for (const key of ['testDir', 'testMatch', 'testIgnore', 'grep', 'grepInvert'] as const) {
        expect(project[key], `${project.name}.${key}`).toBeUndefined()
      }
    }
    expect(config.testDir).toBe('e2e')
    expect(config.grep).toBeUndefined()
  })

  it('keeps the trace and a screenshot of a failure in the CI artifact', () => {
    expect(config.use?.trace).toBe('retain-on-failure')
    expect(config.use?.screenshot).toBe('only-on-failure')
    expect(config.outputDir).toBe('../artifacts/test-results/frontend-e2e/failures')
  })

  it('writes JUnit reports for the verify summary', () => {
    expect(config.reporter).toContainEqual([
      'junit',
      { outputFile: '../artifacts/test-results/frontend-e2e/junit.xml' },
    ])
    expect(scripts.test).toContain('--outputFile.junit=../artifacts/test-results/frontend/junit.xml')
    expect(scripts.e2e).toBe('playwright test')
  })

  it('tests the production build', () => {
    expect(config.webServer).toBeDefined()
    expect(JSON.stringify(config.webServer)).not.toMatch(/\bdev\b/)
  })
})
