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
    await expect(post.getByTestId('post-blog')).toHaveAttribute('href', '/u/anna')
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
    await expect(page).toHaveURL(new RegExp(`${canonical}$`))
    await expectNoHorizontalScroll(page)
  })

  test('адреса других страниц блога постом не считаются', async ({ page }) => {
    await page.goto('/u/anna/comments')
    await expect(page.getByTestId('blog-empty-comments')).toHaveText('Пока нет комментариев')
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
