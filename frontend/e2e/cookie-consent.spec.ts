import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'
import { necessaryCookies as necessary } from '../utils/cookie-consent'

// The cookie banner and settings (E02-13, FR-UI, ADR-0066) in both profiles. Every other test answers the banner
// beforehand (the `cookieChoice` fixture); these start as a first visit. Until a choice, and after «Только
// необходимые», the browser holds only the necessary cookies; the choice survives a reload and is changed at
// /settings/cookies, which is open to guests.
test.use({ cookieChoice: false })

async function cookieNames(page: Page): Promise<string[]> {
  return (await page.context().cookies()).map((cookie) => cookie.name)
}

async function consent(page: Page): Promise<string | undefined> {
  return (await page.context().cookies()).find((cookie) => cookie.name === 'cookie_consent')?.value
}

test.describe('согласие на cookie', () => {
  test('до выбора и после «Только необходимые» ставятся только необходимые cookie, выбор запоминается', async ({
    page,
  }) => {
    await page.goto('/')
    await waitForHydration(page)
    const banner = page.getByRole('region', { name: 'Согласие на cookie' })
    await expect(banner).toBeVisible()
    await expect(banner.getByRole('button', { name: 'Разрешить все' })).toBeVisible()
    await expectNoHorizontalScroll(page)
    // On a phone the banner stays above the bottom bar, so the tabs remain reachable.
    if (isTouch()) {
      const bannerBox = (await banner.boundingBox())!
      const tabBar = (await page.getByTestId('tab-bar').boundingBox())!
      expect(bannerBox.y + bannerBox.height).toBeLessThanOrEqual(tabBar.y)
    }
    for (const name of await cookieNames(page)) expect(necessary).toContain(name)
    expect(await consent(page)).toBeUndefined()

    await press(banner.getByRole('button', { name: 'Только необходимые' }))
    await expect(banner).toHaveCount(0)
    expect(await consent(page)).toBe('v1.analytics-0')
    for (const name of await cookieNames(page)) expect(necessary).toContain(name)

    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('cookie-banner')).toHaveCount(0)
  })

  test('«Разрешить все» записывает согласие на аналитику', async ({ page }) => {
    await page.goto('/')
    await waitForHydration(page)
    await press(
      page.getByRole('region', { name: 'Согласие на cookie' }).getByRole('button', { name: 'Разрешить все' }),
    )
    await expect(page.getByTestId('cookie-banner')).toHaveCount(0)
    expect(await consent(page)).toBe('v1.analytics-1')
  })

  test('«Настроить» открывает настройки, где выбор можно изменить', async ({ page }) => {
    await page.goto('/')
    await waitForHydration(page)
    await press(
      page.getByRole('region', { name: 'Согласие на cookie' }).getByRole('link', { name: 'Настроить' }),
    )
    await page.waitForURL((url) => url.pathname === '/settings/cookies')
    await expect(page.getByRole('heading', { level: 1, name: 'Настройки cookie' })).toBeVisible()
    await expect(page.getByTestId('cookie-banner')).toHaveCount(0)
    await expectNoHorizontalScroll(page)

    const form = page.getByTestId('cookie-settings')
    await expect(form.getByRole('checkbox', { name: 'Необходимые' })).toBeChecked()
    await expect(form.getByRole('checkbox', { name: 'Необходимые' })).toBeDisabled()
    const analytics = form.getByRole('checkbox', { name: 'Аналитические' })
    await expect(analytics).not.toBeChecked()

    await analytics.check()
    await press(form.getByRole('button', { name: 'Сохранить выбор' }))
    await expect(page.getByText('Выбор сохранён')).toBeVisible()
    expect(await consent(page)).toBe('v1.analytics-1')

    // The choice is back on the next visit, and withdrawing it is as easy.
    await page.reload()
    await waitForHydration(page)
    await expect(analytics).toBeChecked()
    await analytics.uncheck()
    await press(form.getByRole('button', { name: 'Сохранить выбор' }))
    await expect.poll(() => consent(page)).toBe('v1.analytics-0')

    await page.goto('/')
    await waitForHydration(page)
    await expect(page.getByTestId('cookie-banner')).toHaveCount(0)
  })

  test('ссылка «Настройки cookie» есть в служебных ссылках', async ({ page }) => {
    await page.goto(isTouch() ? '/menu' : '/')
    await waitForHydration(page)
    const links = isTouch()
      ? page.getByTestId('menu-screen')
      : page.getByRole('navigation', { name: 'Основное меню' })
    await press(links.getByRole('link', { name: 'Настройки cookie' }))
    await page.waitForURL((url) => url.pathname === '/settings/cookies')
  })
})
