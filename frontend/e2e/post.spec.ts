import { expect, expectNoHorizontalScroll, test, waitForHydration } from './fixtures'

// The address of a post (E07-02, FR-POST, ADR-0041) in both profiles against the fake API (e2e/fake-api.mjs):
// `<blog address>/<id>-<slug>`, the same in a personal blog (/u/anna) and in a topic (/books). A wrong slug or blog
// leads to the canonical address with 301, a post without a title opens by its id. The slug itself (transliteration)
// is the API's and is tested by the backend (PostSlugTests).

const titled = '0195f3a0-0000-7000-8000-0000000000c1'
const short = '0195f3a0-0000-7000-8000-0000000000c2'
const inTopic = '0195f3a0-0000-7000-8000-0000000000c3'

test.describe('адрес поста', () => {
  test('пост открывается по адресу блога, id и слагу', async ({ page }) => {
    await page.goto(`/u/anna/${titled}-privet-mir`)
    await waitForHydration(page)
    const post = page.getByTestId('post')
    await expect(post.getByRole('heading', { level: 1, name: 'Привет, мир' })).toBeVisible()
    await expect(post).toContainText('Первый абзац поста.')
    await expect(post.getByTestId('post-author')).toHaveAttribute('href', '/u/anna')
    await expect(page).toHaveURL(new RegExp(`/u/anna/${titled}-privet-mir$`))
    await expect(page).toHaveTitle(/Привет, мир/)
    await expectNoHorizontalScroll(page)
  })

  // E07-03 (#51): the blocks come drawn from the server, with semantic markup, an unknown block as a placeholder.
  test('блоки поста отрисованы семантической разметкой', async ({ page, request }) => {
    const address = `/u/anna/${titled}-privet-mir`
    const html = await (await request.get(address)).text()
    expect(html).toContain('<h2 id="h-b2"')
    expect(html).toContain('data-testid="post-block-unknown"')

    await page.goto(address)
    await waitForHydration(page)
    const body = page.getByTestId('post-blocks')
    await expect(body.getByRole('heading', { level: 2, name: 'Глава первая' })).toHaveAttribute('id', 'h-b2')
    await expect(body.getByRole('heading', { level: 3, name: 'Раздел' })).toBeVisible()
    await expect(body.locator('p b')).toHaveText('жирным')
    await expect(body.getByRole('link', { name: 'ссылкой' })).toHaveAttribute(
      'rel',
      'nofollow ugc noopener noreferrer',
    )
    await expect(body.locator(':scope > ol > li')).toHaveCount(2)
    await expect(body.locator(':scope > ol > li').first().locator('ol > li')).toHaveText('Вложенный')
    await expect(body.locator('figure blockquote')).toHaveText('Краткость — сестра таланта.')
    await expect(body.locator('figure figcaption')).toHaveText('А. П. Чехов')
    await expect(body.getByRole('separator')).toHaveCount(1)
    await expect(body.getByRole('note')).toContainText('не умеем показывать')
    await expectNoHorizontalScroll(page)
  })

  test('неверный слаг и чужой адрес блога ведут на канонический адрес', async ({ page, request }) => {
    const canonical = `/u/anna/${titled}-privet-mir`
    for (const wrong of [
      `/u/anna/${titled}-staryy-zagolovok`,
      `/u/anna/${titled}`,
      `/u/boris/${titled}-privet-mir`,
      `/u/0195f3a0-0000-7000-8000-0000000000b1/${titled}-privet-mir`,
      `/anna/${titled}-privet-mir`,
    ]) {
      const response = await request.get(wrong, { maxRedirects: 0 })
      expect(response.status(), wrong).toBe(301)
      expect(new URL(response.headers().location!, 'http://x').pathname, wrong).toBe(canonical)
    }

    await page.goto(`/u/anna/${titled}-chto-to-ne-to`)
    await page.waitForURL((url) => url.pathname === canonical)
    await expect(page.getByTestId('post-title')).toHaveText('Привет, мир')
  })

  test('пост без заголовка открывается по id', async ({ page, request }) => {
    const response = await request.get(`/u/anna/${short}`, { maxRedirects: 0 })
    expect(response.status()).toBe(200)

    await page.goto(`/u/anna/${short}-lishniy-slag`)
    await page.waitForURL((url) => url.pathname === `/u/anna/${short}`)
    await waitForHydration(page)
    const post = page.getByTestId('post')
    await expect(post).toContainText('Короткий пост без заголовка.')
    await expect(post.getByTestId('post-title')).toHaveCount(0)
    await expectNoHorizontalScroll(page)
  })

  test('у поста в теме та же схема адреса, что и в блоге', async ({ page, request }) => {
    const canonical = `/books/${inTopic}-knigi-oseni`
    const wrong = await request.get(`/u/books/${inTopic}`, { maxRedirects: 0 })
    expect(wrong.status()).toBe(301)
    expect(new URL(wrong.headers().location!, 'http://x').pathname).toBe(canonical)

    await page.goto(canonical)
    await waitForHydration(page)
    await expect(page.getByTestId('post-title')).toHaveText('Книги осени')
    await expect(page.getByTestId('post-blog')).toHaveText('Книги')
    // E07-04: the author of a post in a topic is their own blog, not the topic.
    await expect(page.getByTestId('post-author')).toContainText('Борис')
    await expect(page.getByTestId('post-author')).toHaveAttribute('href', '/u/boris')
    await expect(page).toHaveURL(new RegExp(`${canonical}$`))
    await expectNoHorizontalScroll(page)
  })

  test('адреса других страниц блога постом не считаются', async ({ page }) => {
    await page.goto('/u/anna/comments')
    await expect(page.getByTestId('blog-comments')).toBeVisible()
  })
})

// E07-04 (#52, ADR-0050): the header with the author and the floating action panel, in both profiles.
test.describe('страница поста: шапка и панель действий', () => {
  const address = `/u/anna/${titled}-privet-mir`

  test('шапка и панель приходят с сервера', async ({ request }) => {
    const html = await (await request.get(address)).text()
    expect(html).toContain('data-testid="post-author"')
    expect(html).toContain('Анна')
    expect(html).toContain('<time datetime="2026-11-20T10:00:00Z"')
    expect(html).toMatch(/role="toolbar"[^>]*aria-label="Действия с постом"/)
  })

  test('панель действий закреплена при прокрутке', async ({ page }) => {
    await page.goto(address)
    await waitForHydration(page)
    const panel = page.getByRole('toolbar', { name: 'Действия с постом' })
    // A guest may press it: the sign-in window opens (E10-01, e2e/likes.spec.ts).
    await expect(panel.getByRole('button', { name: 'Нравится' })).toBeEnabled()
    await expect(panel.getByRole('link', { name: 'Комментарии' })).toHaveAttribute('href', '#comments')
    await expect(panel.getByRole('button', { name: 'Поделиться' })).toBeVisible()

    const viewport = page.viewportSize()!
    const tabBar = page.getByTestId('tab-bar')
    // The bottom edge the panel keeps to: the window's, or on a phone the top of the bottom bar.
    const floor = async () => ((await tabBar.isVisible()) ? (await tabBar.boundingBox())!.y : viewport.height)
    for (const scroll of [0, 500, 1000]) {
      await page.evaluate((y) => window.scrollTo(0, y), scroll)
      await expect
        .poll(async () => {
          const box = (await panel.boundingBox())!
          const bottom = await floor()
          return box.y >= 0 && box.y + box.height <= bottom && box.y + box.height >= bottom - 64
        }, `прокрутка ${scroll}`)
        .toBe(true)
    }
    // The post is long enough that the last scroll really happened, so the panel stayed while the text moved.
    expect(await page.evaluate(() => window.scrollY)).toBe(1000)
    await expectNoHorizontalScroll(page)
  })

  // «Поделиться» itself — the system menu, the list and the canonical link — is E07-07, e2e/post-share.spec.ts.
})

test.describe('страница поста: телефон', () => {
  test('на телефоне панель внизу экрана над нижней панелью, «Написать» скрыта', async ({ page }) => {
    await page.goto(`/u/anna/${titled}-privet-mir`)
    await waitForHydration(page)
    const tabBar = page.getByTestId('tab-bar')
    test.skip(!(await tabBar.isVisible()), 'нижняя панель есть только на телефоне')
    const panel = (await page.getByRole('toolbar', { name: 'Действия с постом' }).boundingBox())!
    const bar = (await tabBar.boundingBox())!
    expect(panel.y + panel.height).toBeLessThanOrEqual(bar.y)
    expect(panel.y + panel.height).toBeGreaterThan(bar.y - 32)
    await expect(page.getByTestId('write-button')).toHaveCount(0)
  })
})

test.describe('адрес поста: не найден', () => {
  // The 404 answer is a page load the browser logs as failed.
  const missing = '/u/anna/0195f3a0-0000-7000-8000-0000000000ff-net-takogo'
  test.use({ missingResources: [missing] })

  test('неизвестный пост — «не найден» и 404', async ({ page, request }) => {
    expect((await request.get(missing, { maxRedirects: 0 })).status()).toBe(404)

    await page.goto(missing)
    await expect(page.getByTestId('post-not-found')).toContainText('Пост не найден')
    await expectNoHorizontalScroll(page)
  })
})
