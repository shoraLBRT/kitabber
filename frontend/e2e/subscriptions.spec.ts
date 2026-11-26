import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Subscribing to blogs and topics (E11-03, FR-SUB, ADR-0085) in both profiles against the fake API (e2e/fake-api.mjs,
// which keeps the person's subscriptions per client address): a guest sees the count and the press opens the sign-in
// window, then the subscription is made by itself; a subscription is made and taken back, kept over a reload, on a
// topic and on a blog; the owner sees the numbers without the button; a failure puts the old state back with a toast.
// That subscribing twice is one subscription is tested by the backend (SubscriptionApiTests).

async function asOwnClient(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

/** Presses the button and waits for the server's answer: the page shows the change before it. */
async function pressAndWait(page: Page, button: Locator): Promise<void> {
  const answered = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/bff/subscription',
  )
  await press(button)
  await answered
}

async function open(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('subscription')
}

test.use({ actionTimeout: 10_000 })

test.describe('подписки', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('гость видит число подписчиков, а «Подписаться» открывает окно входа', async ({ page, request }) => {
    const html = await (await request.get('/books')).text()
    expect(html).toContain('41 подписчик')

    const subscription = await open(page, '/books')
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('41 подписчик')
    await press(subscription.getByRole('button', { name: 'Подписаться' }))
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('гость нажимает «Подписаться», входит и подписка оформляется сама', async ({ page }) => {
    const subscription = await open(page, '/books')
    await press(subscription.getByRole('button', { name: 'Подписаться' }))
    const dialog = page.getByRole('dialog', { name: 'Вход' })
    await dialog.getByLabel('Почта').fill('reader@example.com')
    await dialog.getByLabel('Пароль').fill('secret-1')
    const callback = page.waitForRequest((request) => new URL(request.url()).pathname === '/auth/callback')
    await press(dialog.getByRole('button', { name: 'Войти', exact: true }))
    await callback

    await page.waitForURL((url) => url.pathname === '/books')
    await waitForHydration(page)
    await expect(subscription.getByRole('button', { name: 'Вы подписаны' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('42 подписчика')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  test('подписка на тему оформляется, переживает перезагрузку и отменяется', async ({ page }) => {
    await signIn(page)
    const subscription = await open(page, '/books')
    const button = subscription.getByTestId('subscribe-button')
    await expect(button).toHaveText('Подписаться')
    await pressAndWait(page, button)
    await expect(button).toHaveText('Вы подписаны')
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('42 подписчика')

    await page.reload()
    await waitForHydration(page)
    await expect(button).toHaveAttribute('aria-pressed', 'true')
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('42 подписчика')

    await press(button)
    await expect(button).toHaveText('Подписаться')
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('41 подписчик')
    await expectNoHorizontalScroll(page)
  })

  test('подписка на чужой блог видна в счётчике подписок своего', async ({ page }) => {
    await signIn(page)
    const boris = await open(page, '/u/boris')
    await expect(boris.getByTestId('subscription-counts')).toHaveText('0 подписчиков · 0 подписок')
    await pressAndWait(page, boris.getByRole('button', { name: 'Подписаться' }))
    await expect(boris.getByTestId('subscriber-count')).toHaveText('1 подписчик')

    // anna is the person signed in: her own blog has the numbers and no button.
    const own = await open(page, '/u/anna')
    await expect(own.getByTestId('subscription-count')).toHaveText('1 подписка')
    await expect(own.getByTestId('subscribe-button')).toHaveCount(0)
  })

  test('сбой подписки возвращает кнопку и счётчик и показывает тост', async ({ page }) => {
    await signIn(page)
    const subscription = await open(page, '/outage')
    const button = subscription.getByTestId('subscribe-button')
    await press(button)
    await expect(page.getByText('Не удалось изменить подписку. Попробуйте ещё раз.')).toBeVisible()
    await expect(button).toHaveText('Подписаться')
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('0 подписчиков')
  })
})
