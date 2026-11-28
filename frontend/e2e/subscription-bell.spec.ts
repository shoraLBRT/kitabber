import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The bell of a subscription (E11-04, FR-NOTIFY, ADR-0118) in both profiles against the fake API (e2e/fake-api.mjs,
// which keeps the subscriptions and the bells per client address): a guest and a reader who is not subscribed see no
// bell; a subscriber turns it on, it stays on over a reload, and off again; unsubscribing drops it. That a new post
// then notifies the subscriber is tested by the backend (NotificationGenerationTests, NewPostNotificationApiTests).

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

/** Presses the button and waits for the server's answer at `path`: the page shows the change before it. */
async function pressAndWait(page: Page, button: Locator, path: string): Promise<void> {
  const answered = page.waitForResponse((response) => new URL(response.url()).pathname === path)
  await press(button)
  await answered
}

async function open(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('subscription')
}

test.use({ actionTimeout: 10_000 })

test.describe('уведомления о новых постах подписки', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('у гостя и у неподписанного читателя колокольчика нет', async ({ page }) => {
    const asGuest = await open(page, '/books')
    await expect(asGuest.getByTestId('subscribe-button')).toBeVisible()
    await expect(asGuest.getByTestId('subscription-bell')).toHaveCount(0)

    await signIn(page)
    const asReader = await open(page, '/books')
    await expect(asReader.getByTestId('subscribe-button')).toHaveText('Подписаться')
    await expect(asReader.getByTestId('subscription-bell')).toHaveCount(0)
  })

  test('подписчик темы включает колокольчик, он переживает перезагрузку и выключается', async ({ page }) => {
    await signIn(page)
    const subscription = await open(page, '/books')
    await pressAndWait(page, subscription.getByTestId('subscribe-button'), '/bff/subscription')
    const bell = subscription.getByTestId('subscription-bell')
    await expect(bell).toHaveAttribute('aria-pressed', 'false')
    await expect(bell).toHaveAccessibleName('Уведомлять о новых постах')

    await pressAndWait(page, bell, '/bff/subscription-notifications')
    await expect(bell).toHaveAttribute('aria-pressed', 'true')
    await expectNoHorizontalScroll(page)

    await page.reload()
    await waitForHydration(page)
    await expect(bell).toHaveAttribute('aria-pressed', 'true')
    await expect(bell).toHaveAccessibleName('Не уведомлять о новых постах')

    await pressAndWait(page, bell, '/bff/subscription-notifications')
    await expect(bell).toHaveAttribute('aria-pressed', 'false')
  })

  test('отписка от блога убирает колокольчик, новая подписка начинается без него', async ({ page }) => {
    await signIn(page)
    const subscription = await open(page, '/u/boris')
    const button = subscription.getByTestId('subscribe-button')
    await pressAndWait(page, button, '/bff/subscription')
    const bell = subscription.getByTestId('subscription-bell')
    await pressAndWait(page, bell, '/bff/subscription-notifications')
    await expect(bell).toHaveAttribute('aria-pressed', 'true')

    await pressAndWait(page, button, '/bff/subscription')
    await expect(bell).toHaveCount(0)
    await pressAndWait(page, button, '/bff/subscription')
    await expect(bell).toHaveAttribute('aria-pressed', 'false')
    await expectNoHorizontalScroll(page)
  })
})
