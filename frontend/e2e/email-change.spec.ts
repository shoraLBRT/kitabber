import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { APIRequestContext, Page } from '@playwright/test'

// The email change in the settings (E03-06) in both profiles, against the fake API (e2e/fake-api.mjs), which keeps the
// email per client address: the code goes to the new address and the old one stays until it is entered; a taken
// address is refused. That the email changes only with the code, that the old address gets a letter and that a taken
// address does not name its owner is tested by the backend (AccountEmailTests).
const api = 'http://127.0.0.1:3102'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

function newAddress() {
  return `new.${Date.now()}.${Math.random().toString(36).slice(2)}@example.com`
}

async function codeFor(request: APIRequestContext, email: string) {
  let letters: { code: string }[] = []
  await expect
    .poll(async () => {
      letters = await (await request.get(`${api}/__letters?to=${encodeURIComponent(email)}`)).json()
      return letters.length
    })
    .toBeGreaterThanOrEqual(1)
  return letters.at(-1)!.code
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

test.describe('смена почты', () => {
  // Refused addresses and codes are answers of the API, which the browser still logs as failed loads.
  test.use({ missingResources: ['/bff/email', '/bff/email-confirmation'] })

  test('код уходит на новую почту, прежняя остаётся до ввода кода', async ({ page, request }) => {
    await openAccountSettings(page)
    const section = page.getByTestId('email-change')
    await expect(section.getByTestId('email-current')).toContainText('reader@example.com')
    await expectNoHorizontalScroll(page)

    const email = newAddress()
    await section.getByLabel('Новая почта').fill(email)
    await press(section.getByRole('button', { name: 'Получить код' }))
    await expect(section.getByRole('status')).toContainText('Код отправлен на новую почту')
    await expect(section.getByTestId('email-pending')).toContainText(email)
    await expect(section.getByTestId('email-current')).toContainText('reader@example.com')
    await expectNoHorizontalScroll(page)

    const code = await codeFor(request, email)
    const field = section.getByLabel('Код из письма')
    await field.fill(code === '000000' ? '111111' : '000000')
    await press(section.getByRole('button', { name: 'Подтвердить почту' }))
    await expect(field).toHaveAccessibleDescription(/Неверный или просроченный код/)
    await expect(section.getByTestId('email-current')).toContainText('reader@example.com')

    await field.fill(code)
    await press(section.getByRole('button', { name: 'Подтвердить почту' }))
    await expect(section.getByRole('status')).toContainText('На прежний адрес мы отправили письмо')
    await expect(section.getByTestId('email-current')).toContainText(email)

    // The account's email is kept: it is the new one after a reload.
    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('email-current')).toContainText(email)
  })

  test('занятая почта отклоняется', async ({ page }) => {
    await openAccountSettings(page)
    const section = page.getByTestId('email-change')
    await expect(section.getByTestId('email-current')).toContainText('reader@example.com')

    await section.getByLabel('Новая почта').fill('taken@example.com')
    await press(section.getByRole('button', { name: 'Получить код' }))
    await expect(section.getByLabel('Новая почта')).toHaveAccessibleDescription(/Этот адрес уже занят\./)
    await expect(section.getByTestId('email-pending')).toHaveCount(0)
  })
})
