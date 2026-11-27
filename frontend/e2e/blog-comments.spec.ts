import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The tab «Комментарии» of a blog and a topic (E09-15, FR-COMMENT, ADR-0114) in both profiles against the fake API
// (e2e/fake-api.mjs): Анна's blog lists the comments she wrote, newest first, each with the post it is under and a
// link to the comment; «Книги» lists the comments under its posts; «Наука» has none and «Сбой» fails. A signed-in
// person does not see Борис, whom they blocked, after a reload too: the list is read with their token. Deleted
// comments, unpublished posts and the pages themselves are tested by the backend (BlogCommentApiTests).
const autumnBooks = '0195f3a0-0000-7000-8000-0000000000c3'
const annasBlog = '0195f3a0-0000-7000-8000-0000000000b1'
const outage = '0195f3a0-0000-7000-8000-0000000000b6'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

const list = (page: Page) => page.getByTestId('blog-comment')

async function signIn(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

test.describe('вкладка «Комментарии»', () => {
  test('в блоге — комментарии автора со ссылкой на пост, следующие по «Показать ещё»', async ({ page }) => {
    await page.goto('/u/anna/comments')
    await expect(page.getByRole('heading', { level: 1, name: 'Анна' })).toBeVisible()
    await expect(list(page)).toHaveCount(20)
    const first = list(page).first()
    await expect(first.getByTestId('blog-comment-text')).toHaveText('Комментарий 250.4')
    await expect(first.getByTestId('blog-comment-author')).toHaveText('Анна')
    const post = first.getByTestId('blog-comment-post')
    await expect(post).toHaveText('Книги осени')
    await expect(post).toHaveAttribute('href', new RegExp(`/${autumnBooks}-knigi-oseni#comment-`))
    await expectNoHorizontalScroll(page)

    await waitForHydration(page)
    await press(page.getByTestId('blog-comments-more'))
    await expect(list(page)).toHaveCount(40)
    await expect(list(page).nth(20).getByTestId('blog-comment-text')).toHaveText('Комментарий 240.4')

    await press(post)
    await page.waitForURL((url) => url.pathname.includes(autumnBooks))
  })

  test('в теме — вкладки с адресами и комментарии под её постами', async ({ page }) => {
    await page.goto('/books')
    await waitForHydration(page)
    const tabs = page.getByRole('navigation', { name: 'Разделы темы' })
    await expect(tabs.getByRole('link', { name: 'Посты' })).toHaveAttribute('aria-current', 'page')

    await press(tabs.getByRole('link', { name: 'Комментарии' }))
    await page.waitForURL((url) => url.pathname === '/books/comments')
    await expect(tabs.getByRole('link', { name: 'Комментарии' })).toHaveAttribute('aria-current', 'page')
    await expect(list(page).getByTestId('blog-comment-text').first()).toHaveText('Комментарий 250.4')
    await expect(list(page).getByTestId('blog-comment-author').nth(1)).toHaveText('Борис')
    await expect(list(page).getByTestId('blog-comment-post').first()).toHaveText('Книги осени')
    await expectNoHorizontalScroll(page)

    await press(tabs.getByRole('link', { name: 'Посты' }))
    await page.waitForURL((url) => url.pathname === '/books')
    await expect(page.getByTestId('feed-item').first()).toBeVisible()

    // The tab's address opens the tab directly.
    await page.goto('/books/comments')
    await expect(list(page)).toHaveCount(20)
  })

  test('вошедший не видит заблокированного автора и после перезагрузки', async ({ page }) => {
    await signIn(page)
    await page.goto('/books/comments')
    await expect(list(page)).toHaveCount(20)
    await page.reload()
    await expect(list(page)).toHaveCount(20)
    await expect(list(page).getByTestId('blog-comment-author')).toHaveText(Array(20).fill('Анна'))
  })

  test('в теме без комментариев — «Пока нет комментариев»', async ({ page }) => {
    await page.goto('/science/comments')
    await expect(page.getByTestId('blog-comments-empty')).toHaveText('Пока нет комментариев')
    await expectNoHorizontalScroll(page)
  })
})

test.describe('вкладка «Комментарии»: загрузка и ошибка', () => {
  // The tab asks with the blog's id, and a resource is matched by the end of its address.
  test.use({
    missingResources: [`/bff/blog-comments?blog=${annasBlog}`, `/bff/blog-comments?blog=${outage}`],
  })

  test('скелетон, затем ошибка с «Повторить», после неё — список', async ({ page }) => {
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/blog-comments**', async (route) => {
      await held
      await route.fulfill({ status: 502, body: '' })
    })
    await page.goto('/u/anna/comments')
    await expect(page.getByTestId('blog-comments-loading')).toBeVisible()
    await expect(page.getByRole('heading', { level: 1, name: 'Анна' })).toBeVisible()
    release()
    await expect(page.getByTestId('blog-comments-error').getByRole('alert')).toContainText(
      'Не удалось загрузить комментарии',
    )

    await page.unroute('**/bff/blog-comments**')
    await press(page.getByTestId('blog-comments-error').getByRole('button', { name: 'Повторить' }))
    await expect(list(page)).toHaveCount(20)
  })

  test('сбой API — ошибка на месте списка, шапка остаётся', async ({ page }) => {
    await page.goto('/outage/comments')
    await expect(page.getByTestId('topic-title')).toHaveText('Сбой')
    await expect(page.getByTestId('blog-comments-error')).toBeVisible()
  })
})
