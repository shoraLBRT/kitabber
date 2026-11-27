import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { APIRequestContext, Page } from '@playwright/test'

// The onboarding after sign-up (E03-10, FR-AUTH, ADR-0102) in both profiles against the fake API (e2e/fake-api.mjs):
// a new account is led to /onboarding by the sign-in callback, every step can be skipped, the chosen topics become
// subscriptions, and once done it is never offered again. The fake makes only the client of POST /__newcomer a new
// account, so parallel tests and the other specs (an account that has been through it) never see the onboarding. The
// real rules (2–25 characters, whose image, which topics, once) are tested by the backend
// (Platform.Modules.Blogs.IntegrationTests, OnboardingApiTests).
const fakeApi = 'http://127.0.0.1:3102'

async function asNewcomer(page: Page, request: APIRequestContext): Promise<string> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  const client = `10.${octet()}.${octet()}.${octet()}`
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  const marked = await request.post(`${fakeApi}/__newcomer?client=${client}`)
  expect(marked.status()).toBe(204)
  return client
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
}

// The person the fake OIDC provider signs in (the owner of «Анна»); the fake API reads only the token's subject.
const owner = '0195f3a0-0000-7000-8000-000000000001'
const token = `e30.${Buffer.from(JSON.stringify({ sub: owner })).toString('base64url')}.e30`

async function ownProfile(request: APIRequestContext, client: string) {
  const response = await request.get(`${fakeApi}/api/v1/profiles/me`, {
    headers: { 'x-forwarded-for': client, authorization: `Bearer ${token}` },
  })
  return response.json()
}

test.describe('онбординг', () => {
  test('новый аккаунт проходит шаги: имя проверяется, выбранные темы становятся подписками', async ({
    page,
    request,
  }) => {
    const client = await asNewcomer(page, request)
    await signIn(page)
    await page.waitForURL((url) => url.pathname === '/onboarding')
    await waitForHydration(page)
    const steps = page.getByTestId('onboarding')
    await expect(steps.getByRole('heading', { name: 'Как вас зовут?' })).toBeVisible()
    await expectNoHorizontalScroll(page)

    // The name is checked before anything is sent: one character is too short.
    const name = steps.getByRole('textbox', { name: 'Имя' })
    await name.fill('Я')
    await press(steps.getByRole('button', { name: 'Далее' }))
    await expect(steps.getByText('Имя — от 2 до 25 символов.')).toBeVisible()
    await expect(steps.getByRole('heading', { name: 'Как вас зовут?' })).toBeVisible()
    await name.fill('Ян')
    await press(steps.getByRole('button', { name: 'Далее' }))

    await expect(steps.getByRole('heading', { name: 'Аватар' })).toBeVisible()
    await expect(steps.getByText('Шаг 2 из 3')).toBeVisible()
    await press(steps.getByRole('button', { name: 'Пропустить' }))

    await expect(steps.getByRole('heading', { name: 'Выберите темы' })).toBeVisible()
    const books = steps.getByRole('button', { name: /Книги/ })
    await press(books)
    await expect(books).toHaveAttribute('aria-pressed', 'true')
    await expect(steps.getByText('Выбрана 1 тема')).toBeVisible()
    await expectNoHorizontalScroll(page)
    await press(steps.getByRole('button', { name: 'Готово' }))
    await page.waitForURL((url) => url.pathname === '/')

    const own = await ownProfile(request, client)
    expect(own.name).toBe('Ян')
    expect(own.onboardingPending).toBe(false)
    expect(own.subscriptionCount).toBe(1)

    // Never again: neither the page itself nor the next sign-in.
    await page.goto('/onboarding?returnTo=/topics')
    await page.waitForURL((url) => url.pathname === '/topics')
    await page.goto('/auth/sign-in?returnTo=/my')
    await page.waitForURL((url) => url.pathname === '/my')
  })

  test('пропуск каждого шага не мешает войти', async ({ page, request }) => {
    const client = await asNewcomer(page, request)
    await signIn(page)
    await page.waitForURL((url) => url.pathname === '/onboarding')
    await waitForHydration(page)
    const steps = page.getByTestId('onboarding')
    for (const heading of ['Как вас зовут?', 'Аватар', 'Выберите темы']) {
      await expect(steps.getByRole('heading', { name: heading })).toBeVisible()
      await press(steps.getByRole('button', { name: 'Пропустить' }))
    }
    await page.waitForURL((url) => url.pathname === '/')

    const own = await ownProfile(request, client)
    expect(own.onboardingPending).toBe(false)
    expect(own.name).toBeNull()
    expect(own.subscriptionCount).toBe(0)
  })

  test('аккаунт, прошедший онбординг раньше, сразу попадает на страницу', async ({ page }) => {
    await signIn(page)
    await page.waitForURL((url) => url.pathname === '/')
    await page.goto('/onboarding')
    await page.waitForURL((url) => url.pathname === '/')
  })
})
