import { expect, expectNoHorizontalScroll, press, test, waitForHydration } from './fixtures'

// Hashtags (E11-08, FR-TOPIC, ADR-0120) in both profiles against the fake API (e2e/fake-api.mjs): a tag in a post's
// text is a link to the feed of the tag at /tags/<tag>, whatever its case. Which posts carry a tag, the case and the
// order are the backend's and are tested there (HashtagsTests, TagFeedApiTests).

const autumnBooks = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'

test.use({ actionTimeout: 10_000 })

test.describe('хэштеги', () => {
  test('тег в тексте поста ведёт на ленту тега', async ({ page }) => {
    await page.goto(autumnBooks)
    await waitForHydration(page)
    const tag = page.getByTestId('post-blocks').getByRole('link', { name: '#Книги' })
    await expect(tag).toHaveAttribute('href', '/tags/%D0%BA%D0%BD%D0%B8%D0%B3%D0%B8')

    await press(tag)
    await page.waitForURL((url) => decodeURIComponent(url.pathname) === '/tags/книги')
    await expect(page.getByRole('heading', { level: 1, name: '#книги' })).toBeVisible()
    await expect(page.getByTestId('feed-item')).toHaveCount(1)
    await expect(page.getByTestId('feed-item')).toContainText('Книги осени')
    await expectNoHorizontalScroll(page)
  })

  test('регистр тега не различается: адрес ведёт к строчному', async ({ request }) => {
    const response = await request.get('/tags/КНИГИ', { maxRedirects: 0 })
    expect(response.status()).toBe(301)
    expect(decodeURIComponent(response.headers().location ?? '')).toBe('/tags/книги')

    const feed = await (await request.get('/tags/книги')).text()
    expect(feed).toContain('data-testid="feed-list"')
    expect(feed).toContain('Книги осени')
  })

  test('тег без постов — пустая лента; не тег — страница 404', async ({ page, request }) => {
    await page.goto('/tags/никто')
    await expect(page.getByTestId('feed-empty')).toContainText('С этим тегом пока нет постов')

    expect((await request.get('/tags/2026')).status()).toBe(404)
  })

  test.describe('сбой', () => {
    // The refused read of the failing tag logs its 500 to the console; that one is expected.
    test.use({ missingResources: ['/api/v1/feeds/tags/%D0%BA%D0%BD%D0%B8%D0%B3%D0%B8'] })

    test('пока лента читается, видна заглушка; сбой предлагает «Повторить»', async ({ page }) => {
      await page.goto(autumnBooks)
      await waitForHydration(page)
      let fail = true
      let release: () => void = () => {}
      const held = new Promise<void>((resolve) => (release = resolve))
      await page.route('**/api/v1/feeds/tags/**', async (route) => {
        await held
        if (fail) await route.fulfill({ status: 500, json: { status: 500, code: 'internal' } })
        else await route.continue()
      })

      await press(page.getByTestId('post-blocks').getByRole('link', { name: '#Книги' }))
      await page.waitForURL((url) => url.pathname.startsWith('/tags/'))
      await expect(page.getByTestId('feed-loading')).toBeVisible()
      release()
      await expect(page.getByTestId('feed-error')).toBeVisible()

      fail = false
      await press(page.getByTestId('feed-error').getByRole('button', { name: 'Повторить' }))
      await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')
    })
  })
})
