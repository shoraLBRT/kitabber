import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { APIRequestContext, Page } from '@playwright/test'

// The password after registration (E03-05) in both profiles, against the fake API (e2e/fake-api.mjs) and the fake
// provider: the reset from «Забыли пароль?» with the code from the "letter", its errors and the sign-in it ends in;
// the change in the settings with the current password. That a reset ends every earlier session, that the code is
// one-time and short-lived and that the owner gets a letter is tested by the backend against Mailpit (PasswordTests).
const api = 'http://127.0.0.1:3102'

function newAddress() {
  return `owner.${Date.now()}.${Math.random().toString(36).slice(2)}@example.com`
}

async function codeFor(request: APIRequestContext, email: string, count: number) {
  let letters: { code: string }[] = []
  await expect
    .poll(async () => {
      letters = await (await request.get(`${api}/__letters?to=${encodeURIComponent(email)}`)).json()
      return letters.length
    })
    .toBeGreaterThanOrEqual(count)
  return letters.at(-1)!.code
}

// A confirmed account with the password "secret-1", registered through the fake API: its first letter is spent.
async function registered(request: APIRequestContext): Promise<string> {
  const email = newAddress()
  const sent = await request.post(`${api}/api/v1/registration`, { data: { email, password: 'secret-1' } })
  const { registrationId } = await sent.json()
  const code = await codeFor(request, email, 1)
  await request.post(`${api}/api/v1/registration/${registrationId}/confirmation`, { data: { code } })
  return email
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

test.describe('пароль', () => {
  // Refused codes and passwords are answers of the API, which the browser still logs as failed loads.
  test.use({
    missingResources: [
      '/api/v1/password-reset',
      '/confirmation',
      '/bff/password',
      '/missing-avatar.png',
      '/missing-image.svg',
    ],
  })

  test('восстановление по коду из письма: ошибки, затем вход с новым паролем', async ({ page, request }) => {
    const email = await registered(request)
    await page.goto('/login')
    await waitForHydration(page)
    const form = page.getByTestId('sign-in')
    await form.getByLabel('Почта').fill(email)
    await press(form.getByRole('button', { name: 'Забыли пароль?' }))
    await expect(form.getByRole('heading', { name: 'Восстановление пароля' })).toBeVisible()
    await expect(form.getByLabel('Почта')).toHaveValue(email)
    await expectNoHorizontalScroll(page)

    await press(form.getByRole('button', { name: 'Получить код' }))
    await expect(form.getByText(`Если у ${email} есть аккаунт`)).toBeVisible()
    await expect(page.getByTestId('resend-timer')).toContainText(/через 0:0\d/)
    const save = form.getByRole('button', { name: 'Сохранить пароль и войти' })
    await expect(save).toBeDisabled()

    const code = await codeFor(request, email, 2)
    await form.getByLabel('Код из письма').fill(code === '000000' ? '111111' : '000000')
    await form.getByLabel('Новый пароль').fill('brand-new-2')
    await press(save)
    await expect(form.getByLabel('Код из письма')).toHaveAccessibleDescription(
      /Неверный или просроченный код/,
    )

    await form.getByLabel('Код из письма').fill(code)
    await form.getByLabel('Новый пароль').fill('12345')
    await press(save)
    await expect(form.getByLabel('Новый пароль')).toHaveAccessibleDescription(/Не короче 6 символов\./)

    await form.getByLabel('Новый пароль').fill('brand-new-2')
    await press(save)
    await page.waitForURL((url) => url.pathname === '/')
    const cookies = await page.context().cookies()
    expect(cookies.find((cookie) => cookie.name === 'auth.session')?.httpOnly).toBe(true)

    // The new password signs in, the old one does not.
    const old = await request.post(`${api}/api/v1/sign-in/password`, {
      data: { email, password: 'secret-1' },
    })
    expect(old.status()).toBe(400)
    const fresh = await request.post(`${api}/api/v1/sign-in/password`, {
      data: { email, password: 'brand-new-2' },
    })
    expect(fresh.status()).toBe(204)
  })

  test('восстановление открывается и в окне входа', async ({ page }) => {
    await page.goto('/ui')
    await waitForHydration(page)
    await press(page.getByTestId('showcase-auth-gate').getByRole('button', { name: 'Нравится' }))
    const window = page.getByRole('dialog', { name: 'Вход' })
    await press(window.getByRole('button', { name: 'Забыли пароль?' }))
    await expect(window.getByTestId('password-reset')).toBeVisible()
    await press(window.getByRole('button', { name: 'Вернуться ко входу' }))
    await expect(window.getByLabel('Пароль')).toBeVisible()
  })

  test('смена пароля в настройках с вводом текущего', async ({ page }) => {
    await signIn(page)
    await page.goto('/settings/account')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { name: 'Аккаунт и вход' })).toBeVisible()
    await expectNoHorizontalScroll(page)
    const form = page.getByTestId('password-change')
    const submit = form.getByRole('button', { name: 'Сменить пароль' })
    await expect(submit).toBeDisabled()

    await form.getByLabel('Текущий пароль').fill('wrong-password')
    await form.getByLabel('Новый пароль').fill('brand-new-2')
    await press(submit)
    await expect(form.getByLabel('Текущий пароль')).toHaveAccessibleDescription(/Неверный пароль\./)

    await form.getByLabel('Текущий пароль').fill('secret-1')
    await press(submit)
    await expect(form.getByRole('status')).toContainText('Пароль изменён')
  })

  test('гость из настроек аккаунта уходит на вход и возвращается', async ({ page }) => {
    await page.goto('/settings/account')
    await page.waitForURL((url) => url.pathname === '/settings/account')
    await expect(page.getByRole('heading', { name: 'Аккаунт и вход' })).toBeVisible()
  })
})
