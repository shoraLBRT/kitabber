import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The subscriber and subscription lists (E04-06, FR-PROFILE, ADR-0099) in both profiles against the fake API
// (e2e/fake-api.mjs, which keeps the person's subscriptions per client address): the header's numbers open the lists,
// the rows come page by page and there are as many as the number says; the states «загрузка», «пусто», «ошибка»;
// «Подписаться» in a row. The order, the keys of the cursor and the reader's marks are tested by the backend
// (SubscriptionListApiTests).

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

async function open(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('subscription')
}

test.use({ actionTimeout: 10_000 })

test.describe('списки подписчиков и подписок', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('подписчики темы подгружаются по курсору, и их столько, сколько в счётчике', async ({ page }) => {
    const subscription = await open(page, '/books')
    await expect(subscription.getByTestId('subscriber-count')).toHaveText('41 подписчик')
    await press(subscription.getByTestId('subscriber-count'))

    const dialog = page.getByRole('dialog', { name: 'Подписчики' })
    const rows = dialog.getByTestId('subscription-card')
    await expect(rows).toHaveCount(20)
    await press(dialog.getByTestId('subscription-list-more'))
    await expect(rows).toHaveCount(40)
    await press(dialog.getByTestId('subscription-list-more'))
    await expect(rows).toHaveCount(41)
    await expect(dialog.getByTestId('subscription-list-more')).toHaveCount(0)
    await expect(rows.first()).toContainText('Читатель 1')
    await expect(rows.first().getByRole('link')).toHaveAttribute('href', '/u/reader1')
    await expectNoHorizontalScroll(page)
  })

  test('пока список загружается, видны заглушки', async ({ page }) => {
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/subscription-list**', async (route) => {
      await held
      await route.continue()
    })
    const subscription = await open(page, '/books')
    await press(subscription.getByTestId('subscriber-count'))
    const dialog = page.getByRole('dialog', { name: 'Подписчики' })
    await expect(dialog.getByTestId('subscription-list-loading')).toBeVisible()
    release()
    await expect(dialog.getByTestId('subscription-card')).toHaveCount(20)
  })

  test('у блога без подписчиков и подписок — пустые списки', async ({ page }) => {
    const subscription = await open(page, '/u/boris')
    await press(subscription.getByTestId('subscriber-count'))
    const subscribers = page.getByRole('dialog', { name: 'Подписчики' })
    await expect(subscribers).toContainText('Пока никто не подписан.')
    await page.keyboard.press('Escape')
    await expect(subscribers).toHaveCount(0)

    await press(subscription.getByTestId('subscription-count'))
    await expect(page.getByRole('dialog', { name: 'Подписки' })).toContainText('Пока ни на кого не подписан.')
  })

  test('сбой списка показывает ошибку с «Повторить»', async ({ page }) => {
    const subscription = await open(page, '/outage')
    await press(subscription.getByTestId('subscriber-count'))
    const dialog = page.getByRole('dialog', { name: 'Подписчики' })
    await expect(dialog).toContainText('Не удалось загрузить список.')
    const retried = page.waitForResponse(
      (response) => new URL(response.url()).pathname === '/bff/subscription-list',
    )
    await press(dialog.getByTestId('subscription-list-retry'))
    await retried
    await expect(dialog).toContainText('Не удалось загрузить список.')
  })

  test('свои подписки: столько строк, сколько в счётчике, и отписка из строки', async ({ page }) => {
    await signIn(page)
    const books = await open(page, '/books')
    const subscribed = page.waitForResponse(
      (response) => new URL(response.url()).pathname === '/bff/subscription',
    )
    await press(books.getByTestId('subscribe-button'))
    await subscribed

    // The person's own subscription is the newest subscriber, shown by her own blog without a button.
    await press(books.getByTestId('subscriber-count'))
    const subscribers = page.getByRole('dialog', { name: 'Подписчики' })
    const first = subscribers.getByTestId('subscription-card').first()
    await expect(first).toContainText('Анна')
    await expect(first.getByTestId('subscription-card-button')).toHaveCount(0)
    await page.keyboard.press('Escape')

    const own = await open(page, '/u/anna')
    await expect(own.getByTestId('subscription-count')).toHaveText('1 подписка')
    await press(own.getByTestId('subscription-count'))
    const subscriptions = page.getByRole('dialog', { name: 'Подписки' })
    const rows = subscriptions.getByTestId('subscription-card')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('Книги')
    await expect(rows.first()).toContainText('Тема')
    const button = rows.first().getByTestId('subscription-card-button')
    await expect(button).toHaveAttribute('aria-pressed', 'true')
    const unsubscribed = page.waitForResponse(
      (response) => new URL(response.url()).pathname === '/bff/subscription',
    )
    await press(button)
    await unsubscribed
    await expect(button).toHaveText('Подписаться')
    await expectNoHorizontalScroll(page)
  })

  test('гость нажимает «Подписаться» в строке и получает окно входа', async ({ page }) => {
    // A guest's subscriber list of «Книги» has no own row: every row offers «Подписаться».
    const subscription = await open(page, '/books')
    await press(subscription.getByTestId('subscriber-count'))
    const dialog = page.getByRole('dialog', { name: 'Подписчики' })
    await press(dialog.getByTestId('subscription-card-button').first())
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()
  })
})
