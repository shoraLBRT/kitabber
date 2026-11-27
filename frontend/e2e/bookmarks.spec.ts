import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Bookmarks on posts and comments (E10-07, FR-REACT, ADR-0113) in both profiles against the fake API (e2e/fake-api.mjs,
// which keeps the person's bookmarks per client address): put and taken out on a feed card and on the post page, kept
// over a reload and between the two; in a comment's «…» menu; the count only for the author; a guest's press opens the
// sign-in window. The button's states are in tests/bookmarks/bookmarks.spec.ts.

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

async function openPost(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('post-bookmark')
}

/** The card of the post with this title in the feed «Свежее». */
const cardBookmark = (page: Page, title: string) =>
  page.getByTestId('feed-card').filter({ hasText: title }).getByTestId('feed-card-bookmark')

/** Opens the «…» menu of the comment with this text (not of a reply under it) and gives its item named `name`. */
async function commentMenuItem(page: Page, text: string, name: string) {
  const comment = page
    .getByTestId('comments')
    .locator('[data-testid="comment"]')
    .filter({ has: page.locator(':scope > .comment__text', { hasText: text }) })
    .first()
  await press(comment.locator(':scope > .comment__head').getByRole('button', { name: 'Меню комментария' }))
  return isTouch()
    ? page.getByRole('dialog', { name: 'Меню комментария' }).getByRole('button', { name, exact: true })
    : page.getByRole('menu', { name: 'Меню комментария' }).getByRole('menuitem', { name, exact: true })
}

test.use({ actionTimeout: 10_000 })

test.describe('закладки', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('закладка ставится на странице поста, переживает перезагрузку и видна на карточке', async ({
    page,
  }) => {
    await signIn(page)
    const bookmark = await openPost(page, inTopic)
    await expect(bookmark).toHaveAttribute('aria-pressed', 'false')
    await press(bookmark)
    await expect(bookmark).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByText('Добавлено в закладки')).toBeVisible()
    // Someone else's post: no count for the reader.
    await expect(bookmark.getByTestId('bookmark-count')).toHaveCount(0)

    await page.reload()
    await waitForHydration(page)
    await expect(bookmark).toHaveAttribute('aria-pressed', 'true')

    await page.goto('/new')
    await waitForHydration(page)
    const card = cardBookmark(page, 'Книги осени')
    await expect(card).toHaveAttribute('aria-pressed', 'true')
    await press(card)
    await expect(card).toHaveAttribute('aria-pressed', 'false')
    await expectNoHorizontalScroll(page)

    await openPost(page, inTopic)
    await expect(bookmark).toHaveAttribute('aria-pressed', 'false')
    await expectNoHorizontalScroll(page)
  })

  test('автор видит, сколько людей добавили его пост в закладки', async ({ page }) => {
    await signIn(page)
    const bookmark = await openPost(page, titled)
    await expect(bookmark.getByTestId('bookmark-count')).toHaveText('4')
    await press(bookmark)
    await expect(bookmark).toHaveAttribute('aria-pressed', 'true')
    await expect(bookmark.getByTestId('bookmark-count')).toHaveText('5')

    await page.goto('/new')
    await waitForHydration(page)
    await expect(cardBookmark(page, 'Привет, мир').getByTestId('bookmark-count')).toHaveText('5')
    await expect(cardBookmark(page, 'Книги осени').getByTestId('bookmark-count')).toHaveCount(0)
  })

  test('комментарий добавляется в закладки из меню «…», автор видит счётчик', async ({ page }) => {
    await signIn(page)
    await openPost(page, titled)

    await press(await commentMenuItem(page, 'Вторая ветка', 'В закладки'))
    await expect(page.getByText('Добавлено в закладки')).toBeVisible()
    await expect(await commentMenuItem(page, 'Вторая ветка', 'Убрать из закладок')).toBeVisible()
    await page.keyboard.press('Escape')

    // The reply is anna's, the person signed in: two others keep it.
    await expect(await commentMenuItem(page, 'Ответ во второй ветке', 'В закладки · 2')).toBeVisible()
    await page.keyboard.press('Escape')
    await expectNoHorizontalScroll(page)
  })

  test('гость нажимает «В закладки» — открывается окно входа', async ({ page }) => {
    const bookmark = await openPost(page, inTopic)
    await press(bookmark)
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()

    await page.keyboard.press('Escape')
    await page.goto('/new')
    await waitForHydration(page)
    await press(cardBookmark(page, 'Книги осени'))
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('гость нажимает «В закладки», входит и закладка ставится сама', async ({ page }) => {
    const bookmark = await openPost(page, inTopic)
    await press(bookmark)
    const dialog = page.getByRole('dialog', { name: 'Вход' })
    await dialog.getByLabel('Почта').fill('reader@example.com')
    await dialog.getByLabel('Пароль').fill('secret-1')
    const callback = page.waitForRequest((request) => new URL(request.url()).pathname === '/auth/callback')
    await press(dialog.getByRole('button', { name: 'Войти', exact: true }))
    await callback

    await page.waitForURL((url) => url.pathname === new URL(inTopic, 'http://x').pathname)
    await waitForHydration(page)
    await expect(bookmark).toHaveAttribute('aria-pressed', 'true')
  })
})
