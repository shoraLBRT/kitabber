import { expect, test } from '@playwright/test'

// Smoke test of the admin build in both profiles: the page opens without errors, shows the logo in the palette of
// the tokens and does not scroll sideways.
test('админка открывается', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(error.message))
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })

  await page.goto('/')
  await expect(page.locator('header svg').first()).toBeVisible()
  await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(247, 246, 242)')
  const { scrollWidth, innerWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }))
  expect(scrollWidth).toBe(innerWidth)
  expect(errors).toEqual([])
})
