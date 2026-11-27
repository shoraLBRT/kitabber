import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Reposts (E10-08, FR-REACT, ADR-0117) in both profiles against the fake API (e2e/fake-api.mjs, which keeps the
// person's reposts per client address): the list of a post's reposts on its page, a repost with a text from the post
// page, no «Сделать репост» on one's own post or for a guest, and a repost in «Моя лента» of the reposter's subscriber
// with the original inside. Deleting the original hiding its reposts and the author's notification are the backend's
// (RepostApiTests, NotificationGenerationTests); the list's states are in tests/reposts/reposts.spec.ts.

const own = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
const inTopic = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'

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

async function openPost(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('reposts')
}

test.use({ actionTimeout: 10_000 })

test.describe('репосты', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('репост с текстом публикуется со страницы поста и появляется в списке', async ({ page }) => {
    await signIn(page)
    const reposts = await openPost(page, inTopic)
    await expect(reposts.getByTestId('reposts-empty')).toBeVisible()

    await press(reposts.getByTestId('repost-open'))
    await reposts.getByTestId('repost-text').fill('Обязательно к прочтению')
    await press(reposts.getByTestId('repost-submit'))

    await expect(reposts.getByTestId('repost-done')).toHaveText('Репост опубликован в вашем блоге.')
    const item = reposts.getByTestId('repost-item')
    await expect(item).toHaveCount(1)
    await expect(item).toContainText('Анна')
    await expect(item.getByTestId('repost-item-text')).toHaveText('Обязательно к прочтению')
    await expect(reposts.getByTestId('repost-open')).toHaveCount(0)
    await expectNoHorizontalScroll(page)

    // The repost stays after a reload; a second one is refused by the server.
    await page.reload()
    await waitForHydration(page)
    await expect(reposts.getByTestId('repost-item')).toHaveCount(1)
    await press(reposts.getByTestId('repost-open'))
    await press(reposts.getByTestId('repost-submit'))
    await expect(reposts.getByTestId('repost-error')).toHaveText('Вы уже сделали репост этого поста.')
  })

  test('свой пост репостнуть нельзя, но его репосты видны', async ({ page }) => {
    await signIn(page)
    const reposts = await openPost(page, own)

    const item = reposts.getByTestId('repost-item')
    await expect(item).toHaveCount(1)
    await expect(item).toContainText('Борис')
    await expect(item).toContainText('Хороший пост, почитайте.')
    await expect(reposts.getByTestId('repost-open')).toHaveCount(0)
  })

  test('гость видит список, но не кнопку репоста', async ({ page }) => {
    const reposts = await openPost(page, own)

    await expect(reposts.getByTestId('repost-item')).toHaveCount(1)
    await expect(reposts.getByTestId('repost-open')).toHaveCount(0)
  })

  test('репост виден в «Моей ленте» подписчика с оригиналом внутри', async ({ page }) => {
    await signIn(page)
    await page.goto('/u/boris')
    await waitForHydration(page)
    const answered = page.waitForResponse(
      (response) => new URL(response.url()).pathname === '/bff/subscription',
    )
    await press(page.getByTestId('subscription').getByRole('button', { name: 'Подписаться' }))
    await answered

    await page.goto('/my')
    await waitForHydration(page)
    const card = page.getByTestId('feed-card').filter({ has: page.getByTestId('feed-card-repost') })
    await expect(card).toHaveCount(1)
    await expect(card.getByTestId('feed-card-repost')).toContainText('Борис')
    await expect(card.getByTestId('feed-card-repost')).toContainText('сделал репост')
    await expect(card.getByTestId('feed-card-repost-text')).toHaveText('Хороший пост, почитайте.')
    await expect(card.getByTestId('feed-card-title')).toHaveText('Привет, мир')
    await expect(card.getByTestId('feed-card-author')).toContainText('Анна')
    await expectNoHorizontalScroll(page)

    await press(card.getByTestId('feed-card-title'))
    await page.waitForURL((url) => url.pathname === own)
  })
})
