import { expect, test, waitForHydration } from './fixtures'

// The interface strings (E02-10, ADR-0063) in both profiles against the fake API (e2e/fake-api.mjs): the page is in
// the dictionary's locale, every text is resolved (no bare key such as `nav.new` reaches the page) and the dates of
// the feed are written in Russian.

const groups = 'common|nav|shell|errorPage|pages|auth|actions|comments|likes|feed|post|editor|blog|settings'
const bareKey = new RegExp(`\\b(?:${groups})\\.[a-z][A-Za-z]+\\b`)
const russianDate =
  /^\d{1,2} (января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря)/

test('лента — на русском: язык страницы, тексты из словаря, даты по-русски', async ({ page }) => {
  await page.goto('/new')
  await waitForHydration(page)

  await expect(page.locator('html')).toHaveAttribute('lang', 'ru')
  await expect(page.getByRole('heading', { level: 1, name: 'Свежее' })).toBeVisible()
  await expect(page.getByTestId('feed-card').first()).toBeVisible()

  const text = await page.locator('body').innerText()
  expect(text).not.toMatch(bareKey)

  const date = (await page.getByTestId('feed-card-date').first().innerText()).trim()
  expect(date).toMatch(russianDate)
})

test.describe('страница 404', () => {
  // The 404 answer is a page load the browser logs as failed.
  test.use({ missingResources: ['/net-takoj-stranicy'] })

  test('тексты из словаря', async ({ page }) => {
    await page.goto('/net-takoj-stranicy')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Страница не найдена' })).toBeVisible()
    expect(await page.locator('body').innerText()).not.toMatch(bareKey)
  })
})
