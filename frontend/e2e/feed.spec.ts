import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, test, waitForHydration } from './fixtures'

// The fresh feed «Свежее» (E08-01, FR-FEED, ADR-0043) at /new in both profiles against the fake API
// (e2e/fake-api.mjs), which gives its three posts newest first, two per page. The order and the cursor with no
// skipped or repeated post are the backend's and are tested there (FreshFeedApiTests). Infinite loading and coming
// back to the same place are E08-03 (ADR-0051).

// The cursor of the second page of the fake API (the offset 2, base64url).
const secondPage = '/api/v1/feeds/fresh?cursor=Mg'

/** Makes the second page long: posts that exist only in the browser come before the real third post. */
async function longSecondPage(page: Page, count: number): Promise<void> {
  await page.route(`**${secondPage}`, async (route) => {
    const response = await route.fetch()
    const body = (await response.json()) as {
      items: { post: Record<string, unknown> }[]
      nextCursor: string | null
    }
    const real = body.items[0]!
    const fillers = Array.from({ length: count }, (_, n) => ({
      ...real,
      post: {
        ...real.post,
        id: `0195f3a0-0000-7000-8000-00000000f${String(n).padStart(3, '0')}`,
        title: `Пост для прокрутки ${n + 1}`,
        slug: null,
      },
    }))
    await route.fulfill({ response, json: { ...body, items: [...fillers, ...body.items] } })
  })
}

test.describe('лента «Свежее»', () => {
  test('первая страница приходит готовой с сервера', async ({ request }) => {
    const response = await request.get('/new')
    expect(response.status()).toBe(200)
    const html = await response.text()
    expect(html).toContain('data-testid="feed-list"')
    expect(html).toContain('Книги осени')
    expect(html).toContain('Короткий пост без заголовка.')
    expect(html).not.toContain('Привет, мир')
  })

  test('посты идут от новых к старым, следующая страница подгружается сама', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Свежее' })).toBeVisible()
    const items = page.getByTestId('feed-item')
    await expect(items.nth(0)).toContainText('Книги осени')
    await expect(items.nth(1)).toContainText('Короткий пост без заголовка.')

    // The end of a short list is on the screen, so the next page comes without a click; at the end nothing is left.
    await expect(items).toHaveCount(3)
    await expect(items.nth(2)).toContainText('Привет, мир')
    await expect(page.getByTestId('feed-sentinel')).toHaveCount(0)
    await expect(page.getByTestId('feed-more')).toHaveCount(0)
    await expectNoHorizontalScroll(page)
    await expect(page).toHaveTitle(/Свежее/)
  })

  test.describe('сбой подгрузки', () => {
    test.use({ missingResources: [secondPage] })

    test('оставляет посты и даёт повторить', async ({ page }) => {
      let failures = 1
      await page.route(`**${secondPage}`, async (route) => {
        if (failures-- > 0) return route.fulfill({ status: 503, json: { status: 503 } })
        return route.continue()
      })
      await page.goto('/new')
      await waitForHydration(page)
      const items = page.getByTestId('feed-item')
      await expect(page.getByTestId('feed').getByRole('alert')).toContainText(
        'Не удалось загрузить следующие посты',
      )
      await expect(items).toHaveCount(2)
      // A failed page is not asked for again on its own.
      await expect(page.getByTestId('feed-sentinel')).toHaveCount(0)

      await page.getByTestId('feed-more').click()
      await expect(items).toHaveCount(3)
      await expect(items.nth(2)).toContainText('Привет, мир')
      await expect(page.getByTestId('feed').getByRole('alert')).toHaveCount(0)
    })
  })

  // The card (E08-02, ADR-0052): «Книги осени» is boris's post in the topic «Книги»; «Привет, мир» has eight blocks
  // and is cut, the other two are short and shown whole.
  test('карточка: автор, тема, короткий пост целиком, лонгрид с «Показать полностью»', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    const items = page.getByTestId('feed-item')
    await expect(items).toHaveCount(3)

    const inTopic = items.nth(0)
    await expect(inTopic.getByTestId('feed-card-author')).toHaveText(/Борис/)
    await expect(inTopic.getByTestId('feed-card-topic')).toHaveText('Книги')
    await expect(inTopic.getByTestId('feed-card-more')).toHaveCount(0)

    const short = items.nth(1)
    await expect(short.getByRole('heading', { level: 2 })).toHaveCount(0)
    await expect(short).toContainText('Короткий пост без заголовка.')
    await expect(short.getByTestId('feed-card-topic')).toHaveCount(0)
    await expect(short.getByTestId('feed-card-more')).toHaveCount(0)

    const long = items.nth(2)
    await expect(long).toContainText('Первый абзац поста.')
    await expect(long).toContainText('Глава первая')
    await expect(long).not.toContainText('Краткость — сестра таланта.')
    const more = long.getByTestId('feed-card-more')
    await expect(more).toHaveText('Показать полностью')

    // The card fits the column in both profiles: nothing sticks out of it, the page does not scroll sideways.
    for (const card of [inTopic, short, long]) {
      const box = (await card.boundingBox())!
      expect(box.x).toBeGreaterThanOrEqual(0)
      expect(box.x + box.width).toBeLessThanOrEqual(page.viewportSize()!.width)
    }
    await expectNoHorizontalScroll(page)

    await more.click()
    await expect(long).toContainText('Краткость — сестра таланта.')
    await expect(more).toHaveCount(0)
    await expectNoHorizontalScroll(page)
  })

  test('пост открывается из ленты по своему адресу', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    await page.getByRole('link', { name: 'Книги осени' }).click()
    await page.waitForURL((url) => url.pathname === '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni')
    await expect(page.getByTestId('post-title')).toHaveText('Книги осени')
  })

  test('после поста «назад» возвращает ленту на прежнее место', async ({ page }) => {
    await longSecondPage(page, 12)
    await page.goto('/new')
    await waitForHydration(page)
    const items = page.getByTestId('feed-item')
    await expect(items).toHaveCount(15)

    const last = page.getByRole('link', { name: 'Привет, мир' })
    await last.scrollIntoViewIfNeeded()
    const scrolled = await page.evaluate(() => window.scrollY)
    expect(scrolled).toBeGreaterThan(300)

    const feedRequests: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('/feeds/fresh')) feedRequests.push(request.url())
    })
    await last.click()
    await expect(page.getByTestId('post-title')).toHaveText('Привет, мир')

    await page.goBack()
    await expect(page.getByRole('heading', { level: 1, name: 'Свежее' })).toBeVisible()
    // The same posts at once, the loaded pages included, without reading the feed again, and on the same place.
    await expect(items).toHaveCount(15)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrolled)
    await expect(last).toBeInViewport()
    expect(feedRequests).toEqual([])
  })

  test('новый заход в ленту читает её заново с начала', async ({ page }) => {
    await longSecondPage(page, 12)
    await page.goto('/new')
    await waitForHydration(page)
    await page.getByRole('link', { name: 'Привет, мир' }).click()
    await expect(page.getByTestId('post-title')).toHaveText('Привет, мир')

    // A new visit within the site (as by a link of the menu), not «назад»: the feed is read again from the top.
    const firstPage = page.waitForRequest((request) => request.url().endsWith('/api/v1/feeds/fresh'))
    await page.evaluate(() => {
      type Mounted = { __vue_app__: { $nuxt: { $router: { push: (to: string) => Promise<unknown> } } } }
      return (document.querySelector('#__nuxt') as unknown as Mounted).__vue_app__.$nuxt.$router.push('/new')
    })
    await firstPage
    await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')
    await expect(page.getByTestId('feed-item')).toHaveCount(15)
    expect(await page.evaluate(() => window.scrollY)).toBe(0)
  })
})
