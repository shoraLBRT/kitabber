import type { Browser, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// «Ленты» of the settings (E08-13, FR-FEED, ADR-0110) at /settings/feeds in both profiles against the fake API
// (e2e/fake-api.mjs): the home feed chosen on one device is kept in the account, so on another device the home page
// opens «Свежее» from the server; «Популярное» brings the home page back. The rules (defaults, refused values, the
// partial save) are the backend's: PreferenceApiTests.

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

/** Signs in and comes back to the home page, wherever it then leads. */
async function signIn(page: Page, client: string): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  // The home page, or «Свежее» once the account chose it for the home page.
  await page.waitForURL((url) => url.pathname === '/' || url.pathname === '/new')
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

function saved(page: Page) {
  return page.waitForResponse(
    (response) => response.url().includes('/bff/preferences/feeds') && response.request().method() === 'PUT',
  )
}

test.describe('настройки лент', () => {
  test('лента на главной хранится в аккаунте и открывается на другом устройстве', async ({
    page,
    browser,
    baseURL,
  }) => {
    const client = newClient()
    await signIn(page, client)
    await page.goto('/settings/feeds')
    await waitForHydration(page)
    const home = page.getByTestId('feed-settings-home')
    await expect(home.getByRole('radio', { name: 'Популярное' })).toBeChecked()
    await expect(page.getByTestId('feed-settings-video').getByRole('checkbox')).toBeChecked()
    await expectNoHorizontalScroll(page)

    const save = saved(page)
    await home.getByText('Свежее', { exact: true }).click()
    expect((await save).status()).toBe(204)
    await expect(page.getByText('Изменения сохранены')).toBeVisible()

    const phone = await otherDevice(browser, baseURL!)
    try {
      await signIn(phone, client)
      // The site's server reads the account's choice before it renders the home page and sends the person on.
      const response = await phone.goto('/')
      expect(new URL(response!.url()).pathname).toBe('/new')
      await expect(phone.getByTestId('fresh-rating')).toBeVisible()
    } finally {
      await phone.context().close()
    }

    const back = saved(page)
    await home.getByText('Популярное', { exact: true }).click()
    expect((await back).status()).toBe(204)
    await page.goto('/')
    expect(new URL(page.url()).pathname).toBe('/')
  })
})
