import { expect, isTouch, press, test } from './fixtures'

// Sign-in through OpenID Connect (E18-02, ADR-0018) in both profiles: "Войти" leads to the provider (here the fake,
// e2e/fake-oidc-provider.mjs) with PKCE S256, the code is exchanged by the Nuxt server, and the browser ends up with
// one HttpOnly cookie and no token anywhere a script could read it.
const provider = 'http://127.0.0.1:3101'

test.describe('вход', () => {
  test('вход по коду авторизации с PKCE, токены не попадают в браузер', async ({ page, request }) => {
    await page.goto(isTouch() ? '/menu' : '/')
    // Guest state: "Войти" in the header on the desktop, on the "Меню" screen on the phone.
    const login = isTouch()
      ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
      : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
    await expect(login).toBeVisible()

    await press(login)
    await page.waitForURL((url) => url.pathname === '/')

    // Signed in: the account menu instead of "Войти".
    if (isTouch()) {
      await page.goto('/menu')
      await expect(page.getByTestId('menu-screen').getByRole('link', { name: /Аккаунт/ })).toBeVisible()
      await expect(page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })).toHaveCount(0)
    } else {
      await expect(page.getByRole('button', { name: 'Меню профиля: Аккаунт' })).toBeVisible()
      await expect(page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })).toHaveCount(0)
    }

    // The site asked for a code with PKCE S256.
    const asked = (await (await request.get(`${provider}/__last`)).json()) as Record<string, string>
    expect(asked.response_type).toBe('code')
    expect(asked.code_challenge_method).toBe('S256')
    expect(asked.code_challenge).toMatch(/^[\w-]{43}$/)

    // Nothing a script can read holds a token: no web storage, no readable cookie, nothing in the page.
    const visible = await page.evaluate(() => ({
      storage: JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }),
      cookies: document.cookie,
      html: document.documentElement.outerHTML,
    }))
    for (const text of [visible.storage, visible.cookies, visible.html]) {
      expect(text).not.toMatch(/eyJ[\w-]+\.[\w-]+\./)
      expect(text).not.toContain('access_token')
    }
    expect(visible.cookies).not.toContain('auth.session')
    const session = (await page.context().cookies()).find((cookie) => cookie.name === 'auth.session')
    expect(session?.httpOnly).toBe(true)
    expect(session?.sameSite).toBe('Lax')
  })

  test('ошибка входа: обратный вызов без начатого входа не даёт сессии', async ({ request }) => {
    const response = await request.get('/auth/callback?code=forged&state=forged', { maxRedirects: 0 })

    expect(response.status()).toBe(400)
    expect(response.headers()['set-cookie'] ?? '').not.toContain('auth.session')
  })
})
