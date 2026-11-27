import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// «Способы входа» in the settings (#119) in both profiles, against the fake API (e2e/fake-api.mjs), which keeps the
// linked providers per client address: linking through the fake provider and back, a refusal there with its message,
// and unlinking. Which account a provider may be linked to and the last way to sign in that stays are tested by the
// backend (ExternalSignInTests, ExternalIdentityTests) and the component test (tests/settings/SignInMethods.spec.ts).
function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function openAccountSettings(page: Page, client: string): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
  await page.goto('/settings/account')
  await waitForHydration(page)
}

test.describe('способы входа', () => {
  test('привязать через провайдера, отказ у провайдера, отвязать', async ({ page }) => {
    await openAccountSettings(page, newClient())
    const section = page.getByTestId('sign-in-methods')
    const rows = section.getByTestId('sign-in-method')
    await expect(rows).toHaveCount(4)
    await expect(rows.first()).toContainText('Не привязан')
    await expectNoHorizontalScroll(page)

    await press(section.getByRole('link', { name: 'Привязать Google' }))
    await press(page.getByRole('link', { name: 'Отказать' }))
    await page.waitForURL(
      (url) => url.pathname === '/settings/account' && url.search.includes('externalError=denied'),
    )
    await expect(page.getByTestId('sign-in-methods').getByRole('alert')).toHaveText(
      'Вход отменён у провайдера.',
    )

    await press(page.getByTestId('sign-in-methods').getByRole('link', { name: 'Привязать VK' }))
    await press(page.getByRole('link', { name: 'Разрешить' }))
    await page.waitForURL((url) => url.pathname === '/settings/account' && url.search === '')
    await waitForHydration(page)
    const vk = page.getByTestId('sign-in-methods').locator('[data-provider="vk"]')
    await expect(vk).toContainText('Привязан')

    await press(vk.getByRole('button', { name: 'Отвязать VK' }))
    await expect(page.getByTestId('sign-in-methods').getByRole('status')).toHaveText('VK отвязан.')
    await expect(vk).toContainText('Не привязан')
  })
})
