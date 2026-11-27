import type { APIRequestContext, Page } from '@playwright/test'
import { expect, isTouch, makeScrollable, test, waitForHydration } from './fixtures'

// The phone gestures of the feeds (E08-14, FR-FEED, ADR-0111) against the fake API (e2e/fake-api.mjs). On the phone
// (the mobile profile, a touch screen) a finger pulling the feed down from the top reads it again: «Свежее» puts the
// post published meanwhile (POST /__fresh-arrival, seen only by this test's client address) on top, the top of a
// period reads its first page again. The header hides while the page is scrolled down and comes back when it is
// scrolled up, and the «Показать новые» banner follows it up. On the desktop the header never moves.
const api = 'http://127.0.0.1:3102'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function publish(request: APIRequestContext, client: string, title: string): Promise<void> {
  const response = await request.post(`${api}/__fresh-arrival?client=${client}`, { data: { title } })
  expect(response.status()).toBe(201)
}

/** A finger put on the feed and moved `by` pixels down in small steps, then lifted (Chromium's touch input). */
async function pullDown(page: Page, by: number): Promise<void> {
  const cdp = await page.context().newCDPSession(page)
  const x = 195
  const from = 300
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y: from }] })
  for (let step = 1; step <= 10; step++)
    await cdp.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [{ x, y: from + (by * step) / 10 }],
    })
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] })
  await cdp.detach()
}

async function scrollTo(page: Page, y: number): Promise<void> {
  await page.evaluate((top) => window.scrollTo(0, top), y)
  await expect.poll(() => page.evaluate(() => Math.round(window.scrollY))).toBe(y)
}

test.describe('жесты ленты на телефоне', () => {
  test('потянуть «Свежее» вниз — новый пост встаёт наверх', async ({ page, request }) => {
    test.skip(!isTouch(), 'жест «потянуть» есть только на сенсорном экране')
    const client = newClient()
    await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
    await page.goto('/new')
    await waitForHydration(page)
    await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')

    await publish(request, client, 'Пост к обновлению')
    const mark = page.getByTestId('pull-refresh-mark')
    await expect(mark).toHaveAttribute('data-state', 'idle')
    const checked = page.waitForResponse(
      (response) => response.url().includes('/api/v1/feeds/fresh') && !response.url().includes('cursor='),
    )
    await pullDown(page, 300)
    await checked
    await expect(page.getByTestId('feed-item').first()).toContainText('Пост к обновлению')
    await expect(page.getByTestId('feed-list')).toContainText('Книги осени')
    await expect(mark).toHaveAttribute('data-state', 'idle')
    await expect(page.getByTestId('new-posts-show')).toHaveCount(0)
  })

  test('потянуть топ вниз — лента читается заново, короткий жест ничего не делает', async ({ page }) => {
    test.skip(!isTouch(), 'жест «потянуть» есть только на сенсорном экране')
    await page.goto('/top/week')
    await waitForHydration(page)
    await expect(page.getByTestId('feed-item').first()).toBeVisible()

    const reads: string[] = []
    page.on('request', (r) => {
      if (r.url().includes('/api/v1/feeds/top/week')) reads.push(r.url())
    })
    await pullDown(page, 60)
    await expect(page.getByTestId('pull-refresh-mark')).toHaveAttribute('data-state', 'idle')
    expect(reads).toHaveLength(0)

    const read = page.waitForResponse((response) => response.url().includes('/api/v1/feeds/top/week'))
    await pullDown(page, 300)
    await read
    await expect(page.getByTestId('feed-item').first()).toBeVisible()
    await expect(page.getByTestId('pull-refresh-mark')).toHaveAttribute('data-state', 'idle')
  })

  test('шапка прячется при прокрутке вниз и возвращается при прокрутке вверх', async ({ page }) => {
    await page.goto('/new')
    await makeScrollable(page)
    const header = page.getByTestId('shell-header')
    await expect(header).toBeInViewport()

    await scrollTo(page, 800)
    if (!isTouch()) {
      // The desktop header stays where it is.
      await expect(header).toBeInViewport()
      await expect.poll(async () => (await header.boundingBox())?.y).toBe(0)
      return
    }
    await expect(header).not.toBeInViewport()
    await scrollTo(page, 600)
    await expect(header).toBeInViewport()
    await scrollTo(page, 1200)
    await expect(header).not.toBeInViewport()
    await scrollTo(page, 0)
    await expect(header).toBeInViewport()
  })

  test('плашка «Показать новые» поднимается вслед за шапкой', async ({ page, request }) => {
    test.skip(!isTouch(), 'шапка прячется только на телефоне')
    const client = newClient()
    await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
    await page.clock.install()
    await page.goto('/new')
    await makeScrollable(page)
    await publish(request, client, 'Пост для плашки')
    const checked = page.waitForResponse(
      (response) => response.url().includes('/api/v1/feeds/fresh') && !response.url().includes('cursor='),
    )
    await page.clock.fastForward('01:01')
    await checked
    const banner = page.getByTestId('new-posts-show')
    await expect(banner).toBeVisible()

    await scrollTo(page, 800)
    await expect(page.getByTestId('shell-header')).not.toBeInViewport()
    await expect(banner).toBeInViewport()
    // Without the header above it the banner sits at the top of the screen.
    await expect.poll(async () => (await banner.boundingBox())?.y ?? 999).toBeLessThan(40)

    await scrollTo(page, 700)
    await expect(page.getByTestId('shell-header')).toBeInViewport()
    // Under the header again: below its 64 px.
    await expect.poll(async () => (await banner.boundingBox())?.y ?? 0).toBeGreaterThan(64)
  })
})
