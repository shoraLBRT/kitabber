import type { Browser, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The rating filter of «Свежее» (E08-07, FR-FEED, ADR-0106) at /new in both profiles against the fake API
// (e2e/fake-api.mjs), whose posts are rated by the others' likes: none reaches +5, every one reaches −10. The threshold
// goes to the API, which applies it; a guest's choice is kept in the browser, a signed-in person's in the account, so
// another device signed in to it renders the feed with the same threshold from the server. The filter itself (which
// posts pass, the cache per threshold, refused values) is the backend's: FreshFeedRatingApiTests and PreferenceApiTests.

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function signIn(page: Page, client: string): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

/** A second device of the same person: a new browser with the profile's screen and the cookie choice made. */
async function otherDevice(browser: Browser, baseURL: string): Promise<Page> {
  const use = test.info().project.use
  const context = await browser.newContext({
    baseURL,
    viewport: use.viewport,
    hasTouch: use.hasTouch,
    isMobile: use.isMobile,
    userAgent: use.userAgent,
    deviceScaleFactor: use.deviceScaleFactor,
  })
  await context.addCookies([{ name: 'cookie_consent', value: 'v1.analytics-0', url: baseURL }])
  return context.newPage()
}

const option = (page: Page, rating: string) =>
  page.getByTestId('fresh-rating').locator(`[data-rating="${rating}"]`)

test.describe('фильтр «Свежего» по рейтингу', () => {
  test('гость выбирает порог, сервер его применяет, браузер его помнит', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    await expect(page.getByTestId('fresh-rating').getByRole('button')).toHaveText([
      'Все',
      'От −10',
      'От +5',
      'От +10',
    ])
    await expect(option(page, 'all')).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByTestId('feed-list')).toContainText('Книги осени')
    await expectNoHorizontalScroll(page)

    const filtered = page.waitForRequest((request) =>
      request.url().includes('/api/v1/feeds/fresh?minRating=5'),
    )
    await press(option(page, '5'))
    await filtered
    await expect(option(page, '5')).toHaveAttribute('aria-pressed', 'true')
    const empty = page.getByTestId('feed-empty')
    await expect(empty).toContainText('С таким рейтингом постов пока нет')
    await expectNoHorizontalScroll(page)

    await page.reload()
    await waitForHydration(page)
    await expect(option(page, '5')).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByTestId('feed-empty')).toBeVisible()

    await press(page.getByTestId('fresh-rating-all'))
    await expect(option(page, 'all')).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByTestId('feed-list')).toContainText('Книги осени')

    await press(option(page, '-10'))
    await expect(page.getByTestId('feed-list')).toContainText('Книги осени')
    await page.reload()
    await waitForHydration(page)
    await expect(option(page, '-10')).toHaveAttribute('aria-pressed', 'true')
  })

  test('порог вошедшего хранится в аккаунте и действует на другом устройстве с первой страницы', async ({
    page,
    browser,
    baseURL,
  }) => {
    const client = newClient()
    await signIn(page, client)
    await page.goto('/new')
    await waitForHydration(page)
    await expect(option(page, 'all')).toHaveAttribute('aria-pressed', 'true')

    const saved = page.waitForResponse(
      (response) =>
        response.url().includes('/bff/preferences/feeds') && response.request().method() === 'PUT',
    )
    await press(option(page, '10'))
    expect((await saved).status()).toBe(204)
    await expect(page.getByTestId('feed-empty')).toBeVisible()

    const phone = await otherDevice(browser, baseURL!)
    try {
      await signIn(phone, client)
      // The site's server reads the account's threshold before it renders the feed, so the page comes filtered.
      const response = await phone.goto('/new')
      const html = await response!.text()
      expect(html).toMatch(
        /aria-pressed="true"[^>]*data-rating="10"|data-rating="10"[^>]*aria-pressed="true"/,
      )
      expect(html).toContain('data-testid="feed-empty"')
      await waitForHydration(phone)
      await expect(option(phone, '10')).toHaveAttribute('aria-pressed', 'true')

      await press(phone.getByTestId('fresh-rating-all'))
      await expect(phone.getByTestId('feed-list')).toContainText('Книги осени')
    } finally {
      await phone.context().close()
    }

    await page.reload()
    await waitForHydration(page)
    await expect(option(page, 'all')).toHaveAttribute('aria-pressed', 'true')
  })
})
