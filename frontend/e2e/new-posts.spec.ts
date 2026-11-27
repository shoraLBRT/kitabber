import type { APIRequestContext, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, makeScrollable, press, test } from './fixtures'

// «Показать новые» (E08-08, FR-FEED, ADR-0108) at /new in both profiles against the fake API (e2e/fake-api.mjs). A post
// published while the feed is open (POST /__fresh-arrival, seen only by this test's client address) is not put into
// the feed: the page reads the first page again every minute (the browser's clock is moved on) and offers the banner;
// pressing it puts the post on top and scrolls up. The check reads the feed with the same rating threshold, so a new
// post below the threshold is not announced.
const api = 'http://127.0.0.1:3102'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function publish(request: APIRequestContext, client: string, title: string): Promise<void> {
  const response = await request.post(`${api}/__fresh-arrival?client=${client}`, { data: { title } })
  expect(response.status()).toBe(201)
}

async function openFresh(page: Page, client: string): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  await page.clock.install()
  await page.goto('/new')
  await makeScrollable(page)
}

/** The first page is read again: a minute passes on the page's clock, and the check answers. */
async function nextCheck(page: Page, threshold?: string): Promise<void> {
  const checked = page.waitForResponse(
    (response) =>
      response.url().includes('/api/v1/feeds/fresh') &&
      !response.url().includes('cursor=') &&
      (threshold === undefined || response.url().includes(`minRating=${threshold}`)),
  )
  await page.clock.fastForward('01:01')
  await checked
}

test.describe('плашка «Показать новые»', () => {
  test('новый пост не вставляется сам, плашка показывает его и прокручивает вверх', async ({
    page,
    request,
  }) => {
    const client = newClient()
    await openFresh(page, client)
    const list = page.getByTestId('feed-list')
    await expect(list).toContainText('Книги осени')
    const banner = page.getByTestId('new-posts-show')
    await expect(banner).toHaveCount(0)

    await publish(request, client, 'Только что опубликовано')
    await nextCheck(page)
    await expect(banner).toBeVisible()
    await expect(banner).toContainText('Показать новые')
    await expect(banner).toContainText('1 новый пост')
    // The feed stays as it was: the new post is only announced.
    await expect(list).not.toContainText('Только что опубликовано')
    await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')
    await expectNoHorizontalScroll(page)

    // The banner stays in view while the reader is further down the feed.
    await page.evaluate(() => window.scrollTo(0, 300))
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(0)
    await expect(banner).toBeInViewport()

    await press(banner)
    await expect(page.getByTestId('feed-item').first()).toContainText('Только что опубликовано')
    await expect(banner).toHaveCount(0)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
  })

  test('новый пост ниже порога рейтинга плашку не вызывает', async ({ page, request }) => {
    const client = newClient()
    await openFresh(page, client)
    await press(page.getByTestId('fresh-rating').locator('[data-rating="5"]'))
    await expect(page.getByTestId('feed-empty')).toBeVisible()

    await publish(request, client, 'Пост без оценок')
    await nextCheck(page, '5')
    await expect(page.getByTestId('new-posts-show')).toHaveCount(0)

    // At «−10» the same post passes, so the next check announces it.
    await press(page.getByTestId('fresh-rating').locator('[data-rating="-10"]'))
    await expect(page.getByTestId('feed-list')).toContainText('Пост без оценок')
    await publish(request, client, 'Ещё один пост')
    await nextCheck(page, '-10')
    await expect(page.getByTestId('new-posts-show')).toContainText('1 новый пост')
  })
})
