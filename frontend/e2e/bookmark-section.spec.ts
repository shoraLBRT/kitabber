import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The section «Закладки» (E04-12, FR-PROFILE, ADR-0116) in both profiles against the fake API (e2e/fake-api.mjs, which
// keeps the person's bookmarks per client address and starts them with a post and a comment deleted since): the tabs
// «Посты» and «Комментарии» list the person's own bookmarks newest first, a deleted one as a stub in its place; another
// person sees only their own; a guest is asked to sign in. The list's states are in tests/bookmarks/BookmarkSection.spec.ts.

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

async function bookmarkPost(page: Page, path: string): Promise<void> {
  await page.goto(path)
  await waitForHydration(page)
  const bookmark = page.getByTestId('post-bookmark')
  await press(bookmark)
  await expect(bookmark).toHaveAttribute('aria-pressed', 'true')
  // The button turns at once; the toast comes with the server's answer, so the next page load cannot cut the request.
  await expect(page.getByText('Добавлено в закладки')).toBeVisible()
}

async function bookmarkComment(page: Page, text: string): Promise<void> {
  const comment = page
    .getByTestId('comments')
    .locator('[data-testid="comment"]')
    .filter({ has: page.locator(':scope > .comment__text', { hasText: text }) })
    .first()
  await press(comment.locator(':scope > .comment__head').getByRole('button', { name: 'Меню комментария' }))
  const item = isTouch()
    ? page
        .getByRole('dialog', { name: 'Меню комментария' })
        .getByRole('button', { name: 'В закладки', exact: true })
    : page
        .getByRole('menu', { name: 'Меню комментария' })
        .getByRole('menuitem', { name: 'В закладки', exact: true })
  await press(item)
  await expect(page.getByText('Добавлено в закладки')).toBeVisible()
}

test.use({ actionTimeout: 10_000 })

test.describe('раздел «Закладки»', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('посты — по времени добавления, новые сверху, удалённый — заглушкой', async ({ page }) => {
    await signIn(page)
    await bookmarkPost(page, titled)
    await bookmarkPost(page, inTopic)

    await page.goto('/bookmarks')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Закладки' })).toBeVisible()
    await expect(
      page.getByRole('navigation', { name: 'Разделы закладок' }).getByRole('link', { name: 'Посты' }),
    ).toHaveAttribute('aria-current', 'page')
    const items = page.getByTestId('bookmarked-post')
    await expect(items).toHaveCount(3)
    await expect(items.nth(0)).toContainText('Книги осени')
    await expect(items.nth(1)).toContainText('Привет, мир')
    await expect(items.nth(2).getByTestId('bookmark-stub')).toHaveText('Пост удалён или скрыт автором.')
    await expect(items.nth(0).getByTestId('feed-card-bookmark')).toHaveAttribute('aria-pressed', 'true')
    await expectNoHorizontalScroll(page)
  })

  test('комментарии — отдельной вкладкой, со ссылкой на комментарий, удалённый — заглушкой', async ({
    page,
  }) => {
    await signIn(page)
    await page.goto(titled)
    await waitForHydration(page)
    await bookmarkComment(page, 'Вторая ветка')

    await page.goto('/bookmarks')
    await waitForHydration(page)
    await press(
      page.getByRole('navigation', { name: 'Разделы закладок' }).getByRole('link', { name: 'Комментарии' }),
    )
    await page.waitForURL((url) => url.pathname === '/bookmarks/comments')

    const items = page.getByTestId('bookmarked-comment')
    await expect(items).toHaveCount(2)
    await expect(items.nth(0).getByTestId('bookmarked-comment-text')).toHaveText('Вторая ветка')
    await expect(items.nth(0).getByTestId('bookmarked-comment-post')).toHaveAttribute(
      'href',
      /\/0195f3a0-0000-7000-8000-0000000000c1-privet-mir#comment-/,
    )
    await expect(items.nth(1).getByTestId('bookmark-stub')).toHaveText('Комментарий удалён.')
    await expectNoHorizontalScroll(page)
  })

  test('закладки видит только владелец: у другого человека их нет, гостя просят войти', async ({ page }) => {
    await signIn(page)
    await bookmarkPost(page, titled)

    // Another person, from another address, keeps none of these.
    await asOwnClient(page)
    await page.goto('/bookmarks')
    await waitForHydration(page)
    const items = page.getByTestId('bookmarked-post')
    await expect(items).toHaveCount(1)
    await expect(items.first().getByTestId('bookmark-stub')).toBeVisible()

    await page.context().clearCookies()
    await page.goto('/bookmarks')
    await waitForHydration(page)
    await expect(page.getByTestId('bookmarks-guest')).toContainText('Войдите, чтобы видеть свои закладки.')
    await expect(page.getByTestId('bookmarked-post')).toHaveCount(0)
    await expectNoHorizontalScroll(page)
  })
})
