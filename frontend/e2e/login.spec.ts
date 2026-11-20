import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Signing in by email (E03-02) in both profiles, against the fake API (e2e/fake-api.mjs) and the fake provider: the
// empty form, the loading and the error, the password, the code from the "letter" with its resend timer, and the end
// of it all — the provider's session cookie and the site signed in through the usual flow. The real codes, letters,
// limits and the answers that do not reveal an address are tested by the backend against Mailpit (SignInTests).
const api = 'http://127.0.0.1:3102'

function newAddress() {
  return `reader.${Date.now()}.${Math.random().toString(36).slice(2)}@example.com`
}

async function codeFor(request: import('@playwright/test').APIRequestContext, email: string, count = 1) {
  let letters: { code: string }[] = []
  await expect
    .poll(async () => {
      letters = await (await request.get(`${api}/__letters?to=${encodeURIComponent(email)}`)).json()
      return letters.length
    })
    .toBeGreaterThanOrEqual(count)
  return letters.at(-1)!.code
}

async function expectSignedIn(page: import('@playwright/test').Page) {
  await page.waitForURL((url) => url.pathname === '/')
  const cookies = await page.context().cookies()
  expect(cookies.find((cookie) => cookie.name === 'identity.session')?.httpOnly).toBe(true)
  expect(cookies.find((cookie) => cookie.name === 'auth.session')?.httpOnly).toBe(true)
  if (isTouch()) {
    await page.goto('/menu')
    await expect(page.getByTestId('menu-screen').getByRole('link', { name: /Аккаунт/ })).toBeVisible()
  } else {
    await expect(page.getByRole('button', { name: 'Меню профиля: Аккаунт' })).toBeVisible()
  }
}

test.describe('вход', () => {
  // Refused sign-ins are answers of the API, which the browser still logs as failed loads.
  test.use({ missingResources: ['/api/v1/sign-in/password', '/api/v1/sign-in/code', '/confirmation'] })

  test('вход по паролю: пустая форма, ошибка, затем вход в аккаунт', async ({ page }) => {
    await page.goto('/login')
    await waitForHydration(page)
    const form = page.getByTestId('sign-in')
    await expect(form.getByRole('heading', { name: 'Вход' })).toBeVisible()
    await expectNoHorizontalScroll(page)

    // Empty: nothing to submit yet.
    const submit = form.getByRole('button', { name: 'Войти', exact: true })
    await expect(submit).toBeDisabled()

    await form.getByLabel('Почта').fill('reader@example.com')
    await form.getByLabel('Пароль').fill('wrong-password')
    await press(submit)
    await expect(form.getByRole('alert')).toHaveText('Неверная почта или пароль.')

    await form.getByLabel('Пароль').fill('secret-1')
    await press(submit)
    await expectSignedIn(page)
  })

  test('вход по коду из письма с таймером повторной отправки', async ({ page, request }) => {
    await page.goto('/login')
    await waitForHydration(page)
    const form = page.getByTestId('sign-in')
    await press(form.getByRole('button', { name: 'Войти по коду из письма' }))

    await form.getByLabel('Почта').fill('not-an-email')
    await press(form.getByRole('button', { name: 'Получить код' }))
    await expect(form.getByText('Это не похоже на адрес почты.')).toBeVisible()

    const email = newAddress()
    await form.getByLabel('Почта').fill(email)
    await press(form.getByRole('button', { name: 'Получить код' }))
    await expect(form.getByText(`Отправили код на ${email}`)).toBeVisible()
    await expect(page.getByTestId('resend-timer')).toContainText(/через 0:0\d/)
    await expectNoHorizontalScroll(page)

    const first = await codeFor(request, email)
    await form.getByLabel('Код из письма').fill(first === '000000' ? '111111' : '000000')
    await press(form.getByRole('button', { name: 'Войти', exact: true }))
    await expect(form.getByText('Неверный или просроченный код')).toBeVisible()

    const resend = form.getByRole('button', { name: 'Отправить код снова' })
    await expect(resend).toBeVisible({ timeout: 10_000 })
    await press(resend)
    await expect(form.getByText('Отправили новый код')).toBeVisible()

    await form.getByLabel('Код из письма').fill(await codeFor(request, email, 2))
    await press(form.getByRole('button', { name: 'Войти', exact: true }))
    await expectSignedIn(page)
  })
})
