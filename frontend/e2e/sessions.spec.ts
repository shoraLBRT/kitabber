import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The devices in the settings (E03-09) in both profiles, against the fake API (e2e/fake-api.mjs), which keeps the
// devices per client address: this one and two others. That a signed-out device stops working within a minute, that
// another account's device cannot be ended and how a device is named is tested by the backend (AccountSessionTests).
function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function openAccountSettings(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
  await page.goto('/settings/account')
  await waitForHydration(page)
}

test.describe('устройства', () => {
  test('видно устройства и время активности, это устройство отмечено, другие можно отключить', async ({
    page,
  }) => {
    await openAccountSettings(page)
    const section = page.getByTestId('sessions')
    const rows = section.getByTestId('session')
    await expect(rows).toHaveCount(3)
    await expect(rows.first()).toContainText('Chrome, Windows')
    await expect(rows.first()).toContainText('Это устройство')
    await expect(rows.nth(1)).toContainText('Safari, iOS')
    await expect(rows.nth(2)).toContainText('Последняя активность: 10 ноября 2026')
    await expectNoHorizontalScroll(page)

    await press(section.getByRole('button', { name: 'Выйти на устройстве «Safari, iOS»' }))
    await expect(section.getByRole('status')).toHaveText('Устройство отключено.')
    await expect(rows).toHaveCount(2)
    await expect(section).not.toContainText('Safari, iOS')

    // The list is the account's: the device stays signed out after a reload.
    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('sessions').getByTestId('session')).toHaveCount(2)
  })

  test('«Выйти со всех устройств» спрашивает подтверждение и оставляет только это устройство', async ({
    page,
  }) => {
    await openAccountSettings(page)
    const section = page.getByTestId('sessions')
    await expect(section.getByTestId('session')).toHaveCount(3)

    await press(section.getByRole('button', { name: 'Выйти со всех устройств' }))
    const dialog = page.getByRole('dialog', { name: 'Выйти со всех устройств?' })
    await expect(dialog).toBeVisible()
    await expectNoHorizontalScroll(page)
    await press(dialog.getByRole('button', { name: 'Отмена' }))
    await expect(dialog).toBeHidden()
    await expect(section.getByTestId('session')).toHaveCount(3)

    await press(section.getByRole('button', { name: 'Выйти со всех устройств' }))
    await press(page.getByTestId('sign-out-others-confirm'))
    await expect(section.getByRole('status')).toHaveText('Вы вышли на всех устройствах, кроме этого.')
    await expect(section.getByTestId('session')).toHaveCount(1)
    await expect(section.getByTestId('session')).toContainText('Это устройство')
    await expect(section.getByTestId('sessions-only-this')).toBeVisible()
  })
})
