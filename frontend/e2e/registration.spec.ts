import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Registration by email and password (E03-01) in both profiles, against the fake API (e2e/fake-api.mjs) and the fake
// provider: the empty form, errors from the API, the code from the "letter", the resend timer, and the end of it all —
// the provider's session cookie and the site signed in through the usual flow. The real letters, codes and their
// expiry are tested by the backend against Mailpit (RegistrationTests).
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

test.describe('регистрация', () => {
  // Refused registrations and codes are answers of the API, which the browser still logs as failed loads.
  test.use({ missingResources: ['/api/v1/registration', '/confirmation'] })

  test('новый человек регистрируется, подтверждает почту кодом и оказывается в аккаунте', async ({
    page,
    request,
  }) => {
    await page.goto('/register')
    await waitForHydration(page)
    const form = page.getByTestId('registration')
    await expect(form.getByRole('heading', { name: 'Регистрация' })).toBeVisible()
    await expectNoHorizontalScroll(page)

    // Empty: nothing to submit yet.
    const submit = form.getByRole('button', { name: 'Зарегистрироваться' })
    await expect(submit).toBeDisabled()

    const email = newAddress()
    await form.getByLabel('Почта').fill(email)
    await form.getByLabel('Пароль').fill('secret-1')
    await press(submit)

    await expect(form.getByText(`Отправили код на ${email}`)).toBeVisible()
    // The resend timer runs first; the wrong code is refused.
    await expect(page.getByTestId('resend-timer')).toContainText(/через 0:0\d/)
    const first = await codeFor(request, email)
    await form.getByLabel('Код из письма').fill(first === '000000' ? '111111' : '000000')
    await press(form.getByRole('button', { name: 'Подтвердить' }))
    await expect(form.getByText('Неверный или просроченный код')).toBeVisible()

    // Once the timer runs out a new code can be sent; only the new one works.
    const resend = form.getByRole('button', { name: 'Отправить код снова' })
    await expect(resend).toBeVisible({ timeout: 10_000 })
    await press(resend)
    await expect(form.getByText('Отправили новый код')).toBeVisible()
    await expect(page.getByTestId('resend-timer')).toBeVisible()

    await form.getByLabel('Код из письма').fill(await codeFor(request, email, 2))
    await press(form.getByRole('button', { name: 'Подтвердить' }))

    // Signed in: the site came back from the provider with the account.
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
  })

  test('ошибки: короткий пароль, неверный адрес и занятая почта', async ({ page }) => {
    await page.goto('/register')
    await waitForHydration(page)
    const form = page.getByTestId('registration')
    const submit = form.getByRole('button', { name: 'Зарегистрироваться' })

    await form.getByLabel('Почта').fill(newAddress())
    await form.getByLabel('Пароль').fill('12345')
    await press(submit)
    await expect(form.getByText('Пароль — не короче 6 символов.')).toBeVisible()

    await form.getByLabel('Почта').fill('not-an-email')
    await form.getByLabel('Пароль').fill('secret-1')
    await press(submit)
    await expect(form.getByText('Это не похоже на адрес почты.')).toBeVisible()

    await form.getByLabel('Почта').fill('taken@example.com')
    await press(submit)
    await expect(form.getByText('Этот адрес уже занят другим аккаунтом.')).toBeVisible()
    await expect(form.getByLabel('Почта')).toHaveAttribute('aria-invalid', 'true')
    await expectNoHorizontalScroll(page)
  })
})
