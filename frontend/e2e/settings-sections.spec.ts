import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Browser, Page } from '@playwright/test'

// The settings sections (E04-08, #69, docs/ui/settings.md, ADR-0070) in both profiles against the fake API
// (e2e/fake-api.mjs): every section has its own address; the desktop lists the sections in the right column, the
// phone opens them from the list of /settings and comes back with «←»; the theme chosen in «Основные» is kept in the
// account, so another device signed in to it shows the same theme. The fake keeps the settings per client address, so
// two browsers with one `x-forwarded-for` are two devices of one person, and parallel tests never meet. The real rules
// are tested by the backend (Platform.Modules.Preferences.IntegrationTests, PreferenceApiTests).

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

const sections = [
  ['Ленты', '/settings/feeds'],
  ['Основные', '/settings/general'],
  ['Уведомления', '/settings/notifications'],
  ['Приватность и блокировки', '/settings/privacy'],
  ['Аккаунт и вход', '/settings/account'],
] as const

test.describe('разделы настроек', () => {
  test('каждый раздел открывается по своему адресу', async ({ page }) => {
    await signIn(page, newClient())
    for (const [title, path] of sections) {
      await page.goto(path)
      await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()
    }
    await page.goto('/settings/blog')
    await expect(page.getByRole('heading', { level: 1, name: 'Настройки блога' })).toBeVisible()
  })

  test('на десктопе разделы — в правой колонке рядом с открытым', async ({ page }) => {
    test.skip(isTouch(), 'the right column is a desktop column')
    await signIn(page, newClient())
    await page.goto('/settings/general')
    await waitForHydration(page)
    const nav = page.getByTestId('settings-nav-aside')
    await expect(nav).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Основные' })).toHaveAttribute('aria-current', 'page')
    await expect(page.getByTestId('settings-list-back')).toBeHidden()

    await press(nav.getByRole('link', { name: 'Уведомления' }))
    await page.waitForURL((url) => url.pathname === '/settings/notifications')
    await expect(page.getByRole('heading', { level: 1, name: 'Уведомления' })).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Уведомления' })).toHaveAttribute('aria-current', 'page')
  })

  test('на телефоне разделы открываются списком, раздел — отдельным экраном с «←»', async ({ page }) => {
    test.skip(!isTouch(), 'the list is the first screen of a phone')
    await signIn(page, newClient())
    await page.goto('/menu')
    await waitForHydration(page)
    await press(page.getByTestId('menu-screen').getByRole('link', { name: 'Настройки', exact: true }))
    await page.waitForURL((url) => url.pathname === '/settings')

    const list = page.getByTestId('settings-nav-list')
    await expect(list.getByRole('link')).toHaveText([
      'Блог',
      'Ленты',
      'Основные',
      'Уведомления',
      'Приватность и блокировки',
      'Аккаунт и вход',
      'Настройки cookie',
    ])
    for (const row of await list.getByRole('link').all())
      expect((await row.boundingBox())!.height).toBeGreaterThanOrEqual(44)
    await expect(page.getByTestId('settings-nav-aside')).toBeHidden()
    await expectNoHorizontalScroll(page)

    await press(list.getByRole('link', { name: 'Основные' }))
    await page.waitForURL((url) => url.pathname === '/settings/general')
    await expect(page.getByRole('heading', { level: 1, name: 'Основные' })).toBeVisible()
    await expectNoHorizontalScroll(page)
    await press(page.getByTestId('settings-list-back'))
    await page.waitForURL((url) => url.pathname === '/settings')
    await expect(list).toBeVisible()
  })

  test('тема из «Основных» сохраняется в аккаунте и действует на другом устройстве', async ({
    page,
    browser,
    baseURL,
  }) => {
    const client = newClient()
    await signIn(page, client)
    await page.goto('/settings/general')
    await waitForHydration(page)
    const form = page.getByTestId('general-settings')
    await expect(form.getByRole('radio', { name: 'Как в системе' })).toBeChecked()

    await press(form.getByText('Тёмная'))
    await expect(page.getByTestId('toasts-polite')).toContainText('Изменения сохранены')
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')

    const phone = await otherDevice(browser, baseURL!)
    try {
      await signIn(phone, client)
      await expect(phone.locator('html')).toHaveAttribute('data-theme', 'dark')
      await phone.goto('/settings/general')
      await expect(phone.getByTestId('general-settings').getByRole('radio', { name: 'Тёмная' })).toBeChecked()
    } finally {
      await phone.context().close()
    }
  })
})
