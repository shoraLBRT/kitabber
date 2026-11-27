import { expect, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The widget «Популярные комментарии» of the right column (E08-12, FR-FEED, ADR-0105) in both profiles against the fake
// API (e2e/fake-api.mjs), which gives «Вторая ветка» by Борис and «Первый уровень» by Анна and keeps Борис blocked for a
// signed-in person. On the desktop the widget lists them with links to the comments, leaves out the blocked author for
// the signed-in person and shows the loading, error and empty states; a phone has no right column and never asks for
// it. The choice of the day, the cache and the guest comments are tested by the backend (PopularCommentApiTests).
const annasPost = '0195f3a0-0000-7000-8000-0000000000c1'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

const widget = (page: Page) =>
  page.getByTestId('shell-aside').getByRole('region', { name: 'Популярные комментарии' })

async function signIn(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto('/')
  await press(page.getByTestId('shell-header').getByRole('link', { name: 'Войти' }))
  await page.waitForURL((url) => url.pathname === '/')
}

test.describe('виджет «Популярные комментарии»', () => {
  test('десктоп: лучшие комментарии за сутки ведут к комментарию', async ({ page }) => {
    test.skip(isTouch(), 'На телефоне правой колонки нет')
    await page.goto('/')
    const comments = widget(page).getByTestId('popular-comment')
    await expect(comments).toHaveCount(2)
    await expect(widget(page).getByTestId('popular-comment-text')).toHaveText([
      'Вторая ветка',
      'Первый уровень',
    ])
    await expect(widget(page).getByTestId('popular-comment-author')).toHaveText(['Борис', 'Анна'])
    await expect(widget(page).getByTestId('popular-comment-post').first()).toHaveText('Привет, мир')
    await expect(comments.first()).toHaveAttribute('href', new RegExp(`/${annasPost}-privet-mir#comment-`))

    await waitForHydration(page)
    await press(comments.first())
    await page.waitForURL((url) => url.pathname.includes(annasPost))
  })

  test('десктоп: вошедший не видит заблокированного автора', async ({ page }) => {
    test.skip(isTouch(), 'На телефоне правой колонки нет')
    await signIn(page)
    await expect(widget(page).getByTestId('popular-comment-text')).toHaveText(['Первый уровень'])
  })
})

test.describe('виджет «Популярные комментарии»: состояния', () => {
  test.use({ missingResources: ['/bff/popular-comments'] })

  test('десктоп: загрузка, ошибка с «Повторить» и пусто', async ({ page }) => {
    test.skip(isTouch(), 'На телефоне правой колонки нет')
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/popular-comments', async (route) => {
      await held
      await route.fulfill({ status: 502, body: '' })
    })
    await page.goto('/')
    await expect(widget(page).getByTestId('widget-skeleton')).toBeVisible()
    release()
    await expect(widget(page).getByRole('alert')).toContainText('Не удалось загрузить')

    await page.unroute('**/bff/popular-comments')
    await page.route('**/bff/popular-comments', (route) => route.fulfill({ json: { comments: [] } }))
    await press(widget(page).getByRole('button', { name: 'Повторить' }))
    await expect(widget(page)).toContainText('За сутки популярных комментариев пока нет')
  })
})

test.describe('виджет «Популярные комментарии» на телефоне', () => {
  test('телефон: правой колонки нет, и виджет не запрашивается', async ({ page }) => {
    test.skip(!isTouch(), 'Только телефон')
    const asked: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('/bff/popular-comments')) asked.push(request.url())
    })
    await page.goto('/')
    await waitForHydration(page)
    await expect(page.locator('[data-column="aside"]')).toBeHidden()
    expect(asked).toEqual([])
  })
})
