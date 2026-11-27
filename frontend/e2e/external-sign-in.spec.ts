import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Signing in with an external provider (#91) in both profiles, against the fake API's provider (e2e/fake-api.mjs) and
// the fake OpenID Connect provider: the buttons of the providers turned on, the refusal at the provider back on the
// sign-in page with its message, and a sign-in that ends in the provider's session and the site signed in through the
// usual flow. Who gets which account, the attempt cookie and the state are tested by the backend (ExternalSignInTests).
test.describe('вход через провайдера', () => {
  test('на странице входа: отказ у провайдера, затем вход через Google', async ({ page }) => {
    await page.goto('/login')
    await waitForHydration(page)
    const external = page.getByTestId('external-sign-in')
    await expect(external.getByText('Или войдите через')).toBeVisible()
    await expect(external.getByRole('link')).toHaveText(['Яндекс', 'VK', 'Google', 'Telegram'])
    await expectNoHorizontalScroll(page)

    // Refused at the provider: back on the sign-in page with the reason, no session.
    await press(external.getByRole('link', { name: 'Войти через Яндекс' }))
    await expect(page.getByRole('heading', { name: 'Фейковый провайдер «yandex»' })).toBeVisible()
    await press(page.getByRole('link', { name: 'Отказать' }))
    await page.waitForURL(
      (url) => url.pathname === '/login' && url.searchParams.get('externalError') === 'denied',
    )
    await expect(page.getByRole('alert')).toHaveText('Вход отменён у провайдера.')
    expect((await page.context().cookies()).some((cookie) => cookie.name === 'identity.session')).toBe(false)

    await press(page.getByTestId('external-sign-in').getByRole('link', { name: 'Войти через Google' }))
    await press(page.getByRole('link', { name: 'Разрешить' }))
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
})
