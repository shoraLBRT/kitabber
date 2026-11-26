import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Likes on posts and comments (E10-01, FR-REACT, ADR-0061) in both profiles against the fake API (e2e/fake-api.mjs,
// which keeps the person's likes per client address): the counts for a guest, whose press opens the sign-in window and
// is repeated after the sign-in; a like put and taken back, kept over a reload; one's own post and comment only show
// the count, a guest comment has no like. The buttons' states are in tests/likes/likes.spec.ts.

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
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

async function open(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByRole('toolbar', { name: 'Действия с постом' }).getByRole('button', { name: 'Нравится' })
}

/** The comment of the first level with this text, not one of its replies. */
const thread = (page: Page, text: string) =>
  page
    .getByTestId('comments')
    .locator('[data-testid="comment"][aria-level="1"]')
    .filter({ hasText: text })
    .first()

/** The like of the comment itself, not of a reply under it. */
const likeOf = (comment: Locator) => comment.locator(':scope > .comment__actions').getByTestId('comment-like')

test.use({ actionTimeout: 10_000 })

test.describe('лайки', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('гость видит счётчики, а нажатие открывает окно входа', async ({ page }) => {
    const like = await open(page, inTopic)
    await expect(like).toHaveAttribute('aria-pressed', 'false')
    await expect(like).toContainText('3')

    await page.goto(titled)
    await waitForHydration(page)
    const second = likeOf(thread(page, 'Вторая ветка'))
    await expect(second).toContainText('2')
    await expect(second).toHaveAttribute('aria-pressed', 'false')
    const guest = thread(page, 'Комментарий из обсуждения канала')
    await expect(guest).toBeVisible()
    await expect(likeOf(guest)).toHaveCount(0)

    await press(second)
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('лайк поста ставится, переживает перезагрузку и снимается', async ({ page }) => {
    await signIn(page)
    const like = await open(page, inTopic)
    await expect(like).toContainText('3')
    await press(like)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(like).toContainText('4')

    await page.reload()
    await waitForHydration(page)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(like).toContainText('4')

    await press(like)
    await expect(like).toHaveAttribute('aria-pressed', 'false')
    await expect(like).toContainText('3')
    await expectNoHorizontalScroll(page)
  })

  test('свой пост и свой комментарий не лайкаются, чужой комментарий — да', async ({ page }) => {
    await signIn(page)
    const own = await open(page, titled)
    await expect(own).toBeDisabled()

    const second = thread(page, 'Вторая ветка')
    const like = likeOf(second)
    await press(like)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(like).toContainText('3')

    // The replies in «Вторая ветка» are anna's, the person signed in.
    const ownReply = second
      .locator('[data-testid="comment"]')
      .filter({ hasText: 'Ответ во второй ветке' })
      .first()
    await expect(likeOf(ownReply)).toBeDisabled()
  })

  test('гость нажимает «Нравится», входит и лайк ставится сам', async ({ page }) => {
    const like = await open(page, inTopic)
    await press(like)
    const dialog = page.getByRole('dialog', { name: 'Вход' })
    await dialog.getByLabel('Почта').fill('reader@example.com')
    await dialog.getByLabel('Пароль').fill('secret-1')
    const callback = page.waitForRequest((request) => new URL(request.url()).pathname === '/auth/callback')
    await press(dialog.getByRole('button', { name: 'Войти', exact: true }))
    await callback

    await page.waitForURL((url) => url.pathname === new URL(inTopic, 'http://x').pathname)
    await waitForHydration(page)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(like).toContainText('4')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })
})
