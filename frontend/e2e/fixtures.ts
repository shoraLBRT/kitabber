import { expect, test as base, type Locator, type Page } from '@playwright/test'

// Shared fixtures of the e2e tests (docs/TESTING.md): a page that fails the test on any script error or console
// error (hydration mismatches included), and small layout helpers.
export const test = base.extend<{ page: Page; missingResources: string[] }>({
  // Paths a page is expected to fail to load, e.g. the broken avatar of the /ui showcase.
  missingResources: [[], { option: true }],
  page: async ({ page, missingResources }, use) => {
    const errors: string[] = []
    page.on('pageerror', (error) => errors.push(`pageerror: ${error.message}`))
    page.on('console', (message) => {
      const url = message.location().url
      if (missingResources.some((path) => url.endsWith(path))) return
      if (message.type() === 'error' || /hydration/i.test(message.text()))
        errors.push(`console: ${message.text()}`)
    })
    await use(page)
    expect(errors, 'the page must not log errors').toEqual([])
  },
})

export { expect }

/** True in the mobile profile (touch screen): taps instead of clicks. */
export function isTouch(): boolean {
  return test.info().project.use.hasTouch === true
}

export async function press(locator: Locator): Promise<void> {
  if (isTouch()) await locator.tap()
  else await locator.click()
}

export async function box(locator: Locator) {
  const rect = await locator.boundingBox()
  expect(rect, 'the element must be laid out').not.toBeNull()
  return rect!
}

/** The page does not scroll sideways: the document is exactly as wide as the window. */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const { scrollWidth, innerWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }))
  expect(scrollWidth).toBe(innerWidth)
}

/** Makes the page tall enough to scroll (the feed is still a placeholder). */
export async function makeScrollable(page: Page): Promise<void> {
  await page.evaluate(() => {
    const spacer = document.createElement('div')
    spacer.style.height = '3000px'
    spacer.dataset.testid = 'spacer'
    document.querySelector('main')!.append(spacer)
  })
}
