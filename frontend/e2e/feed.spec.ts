import { expect, expectNoHorizontalScroll, test, waitForHydration } from './fixtures'

// The fresh feed «Свежее» (E08-01, FR-FEED, ADR-0043) at /new in both profiles against the fake API
// (e2e/fake-api.mjs), which gives its three posts newest first, two per page. The order and the cursor with no
// skipped or repeated post are the backend's and are tested there (FreshFeedApiTests).

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

  test('посты идут от новых к старым, следующая страница — по кнопке', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Свежее' })).toBeVisible()
    const items = page.getByTestId('feed-item')
    await expect(items).toHaveCount(2)
    await expect(items.nth(0)).toContainText('Книги осени')
    await expect(items.nth(1)).toContainText('Короткий пост без заголовка.')
    await expectNoHorizontalScroll(page)

    await page.getByTestId('feed-more').click()
    await expect(items).toHaveCount(3)
    await expect(items.nth(2)).toContainText('Привет, мир')
    await expect(page.getByTestId('feed-more')).toHaveCount(0)
    await expect(page).toHaveTitle(/Свежее/)
  })

  test('пост открывается из ленты по своему адресу', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    await page.getByRole('link', { name: 'Книги осени' }).click()
    await page.waitForURL((url) => url.pathname === '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni')
    await expect(page.getByTestId('post-title')).toHaveText('Книги осени')
  })
})
