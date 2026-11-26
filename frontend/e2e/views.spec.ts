import type { Page, Request } from '@playwright/test'
import { expect, press, test, waitForHydration } from './fixtures'

// The view counter (E07-06, FR-POST, ADR-0071) in both profiles against the fake API (e2e/fake-api.mjs), whose post
// «Привет, мир» has 1 234 views: the count on the card, and what the browser reports to the site's server — the
// opening of a post, the cards a feed showed, and the reading time only with the «Аналитические» consent. Counting a
// reader once a day and leaving bots out is the API's and is tested there (PostViewApiTests).
const titled = '0195f3a0-0000-7000-8000-0000000000c1'

type Report = { kind: string; postId?: string; postIds?: string[]; seconds?: number }

function reportOf(request: Request): Report | null {
  if (!request.url().endsWith('/bff/views') || request.method() !== 'POST') return null
  return request.postDataJSON() as Report
}

function waitForReport(page: Page, kind: string): Promise<Report> {
  return page
    .waitForRequest((request) => reportOf(request)?.kind === kind)
    .then((request) => reportOf(request)!)
}

/** Opens «Привет, мир» from the fresh feed, in the browser, so leaving it with «Назад» stays in the app. */
async function openFromFeed(page: Page): Promise<void> {
  await page.goto('/new')
  await waitForHydration(page)
  const card = page.getByTestId('feed-card').filter({ hasText: 'Привет, мир' })
  await press(card.getByTestId('feed-card-title'))
  await expect(page.getByTestId('post')).toBeVisible()
}

test.describe('просмотры', () => {
  test('карточка показывает счётчик просмотров, лента сообщает о показанных карточках', async ({ page }) => {
    const impressions = waitForReport(page, 'impressions')
    await page.goto('/new')
    await waitForHydration(page)

    const card = page.getByTestId('feed-card').filter({ hasText: 'Привет, мир' })
    await card.scrollIntoViewIfNeeded()
    await expect(card.getByTestId('feed-card-views')).toHaveText(/1\s234 просмотра/)
    await expect(page.getByTestId('feed-card').first().getByTestId('feed-card-views')).toHaveText(
      '0 просмотров',
      { useInnerText: true },
    )

    const report = await impressions
    expect(report.postIds?.length).toBeGreaterThan(0)
  })

  test('открытие поста засчитывается, время чтения без согласия не уходит', async ({ page }) => {
    const reports: Report[] = []
    page.on('request', (request) => {
      const report = reportOf(request)
      if (report) reports.push(report)
    })
    const opened = waitForReport(page, 'open')
    await openFromFeed(page)

    expect(await opened).toEqual({ kind: 'open', postId: titled })
    await page.waitForTimeout(1500)
    await page.goBack()
    await expect(page).toHaveURL(/\/new$/)
    await expect(page.getByTestId('feed-card').first()).toBeVisible()
    expect(reports.filter((r) => r.kind === 'reading')).toEqual([])
  })
})

test.describe('просмотры с согласием на аналитику', () => {
  test.use({ cookieChoice: false })

  test('время чтения уходит, когда читатель покидает пост', async ({ page, baseURL }) => {
    await page.context().addCookies([{ name: 'cookie_consent', value: 'v1.analytics-1', url: baseURL! }])
    await openFromFeed(page)
    await page.waitForTimeout(1500)

    const reading = waitForReport(page, 'reading')
    await page.goBack()

    const report = await reading
    expect(report.postId).toBe(titled)
    expect(report.seconds).toBeGreaterThanOrEqual(1)
  })
})
