import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The blog page (E04-02, FR-PROFILE, docs/ui/blog.md) in both profiles against the fake API (e2e/fake-api.mjs): the
// server renders it for a guest, the tabs switch without a reload and keep their addresses, the owner sees the
// registration date and the management buttons, and the states «не найден» and «ошибка». The real profile rules
// (who sees `registeredAt`, 404, nickname form) are tested by the backend (Platform.Modules.Blogs.IntegrationTests).

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

test.describe('страница блога', () => {
  test('сервер отдаёт гостю готовую страницу блога', async ({ page, request }) => {
    const html = await (await request.get('/u/anna')).text()
    expect(html).toContain('<h1 class="blog__title"')
    expect(html).toContain('Анна')
    expect(html).toContain('@anna')
    expect(html).toContain('Пишу о книгах и городах.')
    expect(html).toContain('Здесь пока нет постов')
    expect(html).not.toContain('На сайте с')

    await page.goto('/u/anna')
    await waitForHydration(page)
    const blog = page.getByTestId('blog')
    await expect(blog.getByRole('heading', { level: 1, name: 'Анна' })).toBeVisible()
    await expect(blog.getByTestId('blog-nickname')).toHaveText('@anna')
    await expect(blog.getByTestId('blog-registered')).toHaveCount(0)
    await expect(blog.getByRole('group', { name: 'Управление блогом' })).toHaveCount(0)
    await expect(blog.getByRole('link', { name: 'Написать первый пост' })).toHaveCount(0)
    await expect(page).toHaveTitle(/Анна/)
    await expectNoHorizontalScroll(page)
  })

  test('вкладки переключаются без перезагрузки и имеют адреса', async ({ page }) => {
    await page.goto('/u/anna')
    await waitForHydration(page)
    await page.evaluate(() => ((window as unknown as { kept: boolean }).kept = true))
    const tabs = page.getByRole('navigation', { name: 'Разделы блога' })
    await expect(tabs.getByRole('link', { name: 'Посты' })).toHaveAttribute('aria-current', 'page')

    await press(tabs.getByRole('link', { name: 'Комментарии' }))
    await page.waitForURL((url) => url.pathname === '/u/anna/comments')
    await expect(page.getByTestId('blog-comments')).toBeVisible()
    await expect(tabs.getByRole('link', { name: 'Комментарии' })).toHaveAttribute('aria-current', 'page')
    expect(await page.evaluate(() => (window as unknown as { kept?: boolean }).kept)).toBe(true)

    await page.goBack()
    await page.waitForURL((url) => url.pathname === '/u/anna')
    await expect(page.getByTestId('blog-empty-posts')).toContainText('Здесь пока нет постов')
    expect(await page.evaluate(() => (window as unknown as { kept?: boolean }).kept)).toBe(true)

    // The tab's address opens the tab directly.
    await page.goto('/u/anna/comments')
    await expect(page.getByTestId('blog-comments')).toBeVisible()
    await expect(page.getByRole('heading', { level: 1, name: 'Анна' })).toBeVisible()
  })

  test('владелец видит дату регистрации и кнопки управления, в чужом блоге — нет', async ({ page }) => {
    await signIn(page)
    await page.goto('/u/anna')
    await waitForHydration(page)
    const blog = page.getByTestId('blog')
    await expect(blog.getByTestId('blog-registered')).toHaveText('На сайте с 14 марта 2026 г.')
    const actions = blog.getByRole('group', { name: 'Управление блогом' })
    for (const name of ['Ред.', 'Настройки'])
      await expect(actions.getByRole('link', { name, exact: true })).toHaveAttribute('href', '/settings/blog')
    await expect(actions.getByRole('button', { name: 'Статистика', exact: true })).toBeDisabled()
    await expect(blog.getByRole('link', { name: 'Написать первый пост' })).toHaveAttribute('href', '/write')
    await expectNoHorizontalScroll(page)

    // The tab switch in the browser keeps the owner's view.
    await press(
      page.getByRole('navigation', { name: 'Разделы блога' }).getByRole('link', { name: 'Комментарии' }),
    )
    await page.waitForURL((url) => url.pathname === '/u/anna/comments')
    await expect(blog.getByTestId('blog-registered')).toBeVisible()

    await page.goto('/u/boris')
    await expect(page.getByRole('heading', { level: 1, name: 'Борис' })).toBeVisible()
    await expect(page.getByTestId('blog-registered')).toHaveCount(0)
    await expect(page.getByRole('group', { name: 'Управление блогом' })).toHaveCount(0)

    // «Профиль» of the menu opens the own blog.
    await page.goto('/profile')
    await page.waitForURL((url) => url.pathname === '/u/anna')
    await expect(page.getByTestId('blog-registered')).toBeVisible()
  })

  test('один адрес у блога; блог без никнейма открывается по идентификатору', async ({ page }) => {
    await page.goto('/u/@Anna')
    await page.waitForURL((url) => url.pathname === '/u/anna')
    await page.goto('/u/0195f3a0-0000-7000-8000-0000000000b1/comments')
    await page.waitForURL((url) => url.pathname === '/u/anna/comments')

    await page.goto('/u/0195f3a0-0000-7000-8000-0000000000b3')
    await expect(page.getByRole('heading', { level: 1, name: 'Блог без названия' })).toBeVisible()
    await expect(page.getByTestId('blog-nickname')).toHaveCount(0)
  })
})

test.describe('страница блога: ошибки', () => {
  // The 404 and 500 answers are page loads the browser logs as failed.
  test.use({ missingResources: ['/u/nobody', '/u/id123', '/u/broken'] })

  test('неизвестный блог — 404, сбой API — ошибка с «Повторить»', async ({ page }) => {
    for (const path of ['/u/nobody', '/u/id123']) {
      const response = await page.goto(path)
      expect(response?.status()).toBe(404)
      await expect(
        page.getByTestId('blog-not-found').getByRole('heading', { name: 'Блог не найден' }),
      ).toBeVisible()
    }

    // A failure of the API answers 500 (E02-07, ADR-0057).
    expect((await page.goto('/u/broken'))?.status()).toBe(500)
    await waitForHydration(page)
    const failure = page.getByTestId('blog-error')
    await expect(failure.getByRole('heading', { name: 'Не удалось загрузить блог' })).toBeVisible()
    await press(failure.getByRole('button', { name: 'Повторить' }))
    // The fake keeps failing, so the retry ends in the same state.
    await expect(page.getByTestId('blog-error')).toBeVisible()
    await expectNoHorizontalScroll(page)
  })
})
