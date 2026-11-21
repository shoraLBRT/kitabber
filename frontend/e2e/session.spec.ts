import type { Page } from '@playwright/test'
import { expect, isTouch, press, test } from './fixtures'

// The site's session (E03-03, ADR-0024) in both profiles, against the fake provider (e2e/fake-oidc-provider.mjs),
// whose access tokens live 3 seconds, and the fake API, which refuses an expired one: the session outlives its first
// token without the person noticing, and "Выйти" revokes the refresh token, ends the provider's session and leaves a
// guest.
const provider = 'http://127.0.0.1:3101'

interface ProviderLog {
  refreshes: number
  revocations: string[]
  endSessions: Record<string, string>[]
}

async function providerLog(page: Page): Promise<ProviderLog> {
  return (await (await page.request.get(`${provider}/__log`)).json()) as ProviderLog
}

async function signIn(page: Page) {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function expectSignedIn(page: Page, signedIn: boolean) {
  if (isTouch()) {
    await page.goto('/menu')
    const screen = page.getByTestId('menu-screen')
    await expect(screen.getByRole('link', { name: 'Войти' })).toHaveCount(signedIn ? 0 : 1)
    await expect(screen.getByRole('button', { name: 'Выйти' })).toHaveCount(signedIn ? 1 : 0)
  } else {
    const header = page.getByTestId('shell-header')
    await expect(header.getByRole('link', { name: 'Войти' })).toHaveCount(signedIn ? 0 : 1)
    await expect(page.getByRole('button', { name: 'Меню профиля: Аккаунт' })).toHaveCount(signedIn ? 1 : 0)
  }
}

const ticket = (page: Page) => page.request.post('/auth/realtime-ticket')

test.describe('сессия', () => {
  test('истёкший токен доступа обновляется незаметно', async ({ page }) => {
    await signIn(page)
    const before = await providerLog(page)

    // The first access token is long expired; the fake API would refuse it.
    await page.waitForTimeout(4_000)
    await page.reload()

    await expectSignedIn(page, true)
    expect((await ticket(page)).status()).toBe(200)
    expect((await providerLog(page)).refreshes).toBeGreaterThan(before.refreshes)
  })

  test('выход отзывает refresh-токен и завершает сессию у провайдера', async ({ page }) => {
    await signIn(page)
    const before = await providerLog(page)

    if (isTouch()) {
      await page.goto('/menu')
      await press(page.getByTestId('menu-screen').getByRole('button', { name: 'Выйти' }))
    } else {
      await press(page.getByRole('button', { name: 'Меню профиля: Аккаунт' }))
      await press(page.getByRole('menuitem', { name: 'Выйти' }))
    }
    await page.waitForURL((url) => url.pathname === '/')

    await expectSignedIn(page, false)
    expect((await page.context().cookies()).some((cookie) => cookie.name === 'auth.session')).toBe(false)
    expect((await ticket(page)).status()).toBe(401)
    const after = await providerLog(page)
    expect(after.revocations.length).toBeGreaterThan(before.revocations.length)
    const ended = after.endSessions.slice(before.endSessions.length)
    expect(
      ended.some(
        (query) => query.post_logout_redirect_uri === 'http://127.0.0.1:3100/' && !!query.id_token_hint,
      ),
    ).toBe(true)
  })
})
