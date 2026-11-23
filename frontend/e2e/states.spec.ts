import type { Locator } from '@playwright/test'
import { box, expect, expectNoHorizontalScroll, press, test, waitForHydration } from './fixtures'

// The shared states of every screen (E02-07, FR-UI, docs/ui/README.md, ADR-0057) in both profiles against the fake API
// (e2e/fake-api.mjs): the 404 and 500 answers of the server render, the «Нет подключения» bar with the failed content
// read again when the network comes back, and skeletons of the size of the content they stand for.

const nowhere = '/net-takoj-stranicy'

test.describe('страницы 404 и 500', () => {
  // The 404 and 500 answers are page loads the browser logs as failed.
  test.use({ missingResources: [nowhere, '/u/broken'] })

  test('адрес без страницы — 404 с «Страница не найдена» в каркасе', async ({ page, request }) => {
    const answer = await request.get(nowhere)
    expect(answer.status()).toBe(404)
    const html = await answer.text()
    expect(html).toContain('Страница не найдена')
    expect(html).toContain('data-testid="shell"')

    await page.goto(nowhere)
    await waitForHydration(page)
    const missing = page.getByTestId('not-found-page')
    await expect(missing.getByRole('heading', { level: 1, name: 'Страница не найдена' })).toBeVisible()
    await expect(page).toHaveTitle(/Страница не найдена/)
    await expectNoHorizontalScroll(page)

    await press(missing.getByRole('link', { name: 'На главную' }))
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByTestId('not-found-page')).toHaveCount(0)
  })

  test('сбой API при серверном рендеринге — 500 с ошибкой и «Повторить»', async ({ request }) => {
    const answer = await request.get('/u/broken')
    expect(answer.status()).toBe(500)
    const html = await answer.text()
    expect(html).toContain('data-testid="blog-error"')
    expect(html).toContain('Повторить')
    expect(html).toContain('data-testid="shell"')
  })
})

test('потеря сети — плашка «Нет подключения», при возвращении несостоявшееся грузится снова', async ({
  page,
  context,
}) => {
  await page.goto('/new')
  await waitForHydration(page)
  await expect(page.getByTestId('offline-banner')).toHaveCount(0)

  // The post fails to load, as it does when the network drops on the way.
  await page.route('**/bff/post?**', (route) => route.fulfill({ json: { status: 'error' } }))
  await press(page.getByTestId('feed-card-title').first())
  await expect(page.getByTestId('post-error')).toBeVisible()
  await page.unroute('**/bff/post?**')

  await context.setOffline(true)
  const banner = page.getByTestId('offline-banner')
  await expect(banner).toBeVisible()
  await expect(banner).toHaveText(/Нет подключения/)
  await expect(banner).toHaveAttribute('role', 'status')
  // The bar stands above the content of the central column.
  expect((await box(banner)).y).toBeLessThan((await box(page.getByTestId('post-error'))).y)
  await expectNoHorizontalScroll(page)

  await context.setOffline(false)
  await expect(banner).toHaveCount(0)
  await expect(page.getByTestId('post')).toBeVisible()
  await expect(page.getByTestId('post-error')).toHaveCount(0)
})

/** The height of one line of the text of an element, as its style sets it. */
async function lineHeight(locator: Locator): Promise<number> {
  return locator.evaluate((element) => parseFloat(getComputedStyle(element).lineHeight))
}

test('скелетон карточки совпадает по размерам с карточкой ленты', async ({ page }) => {
  // The second page waits until the skeleton under the list is measured.
  let release = () => {}
  const held = new Promise<void>((resolve) => (release = resolve))
  await page.route('**/api/v1/feeds/fresh?cursor=Mg', async (route) => {
    await held
    await route.continue()
  })
  await page.goto('/new')
  await waitForHydration(page)

  const skeleton = page.getByTestId('feed-loading-more')
  await expect(skeleton).toBeVisible()
  const card = page.getByTestId('feed-item').first()

  // The same card: the same column, the same width and fields.
  const [skeletonBox, cardBox] = [await box(skeleton), await box(card)]
  expect(skeletonBox.x).toBe(cardBox.x)
  expect(skeletonBox.width).toBe(cardBox.width)
  const inner = (locator: Locator) =>
    locator.evaluate((element) => parseFloat(getComputedStyle(element).paddingLeft))
  expect(await inner(skeleton)).toBe(await inner(card))

  // The avatar of the author.
  const avatar = await box(card.getByTestId('feed-card-author').locator('.ui-avatar'))
  const avatarSkeleton = await box(skeleton.locator('.ui-skeleton--avatar'))
  expect(avatarSkeleton.width).toBe(avatar.width)
  expect(avatarSkeleton.height).toBe(avatar.height)

  // The title of one line («Книги осени»).
  const title = card.getByTestId('feed-card-title').locator('h2')
  const titleHeight = (await box(title)).height
  expect(titleHeight).toBeCloseTo(await lineHeight(title), 0)
  expect((await box(skeleton.getByTestId('feed-card-skeleton-title'))).height).toBeCloseTo(titleHeight, 0)

  // A line of the text of the post.
  const paragraph = card.locator('.post-blocks p').first()
  const textLines = skeleton.locator('.ui-skeleton--text-reading')
  await expect(textLines).toHaveCount(2)
  expect((await box(textLines.first())).height).toBeCloseTo(await lineHeight(paragraph), 0)

  release()
  await expect(skeleton).toHaveCount(0)
  await expect(page.getByTestId('feed-item')).toHaveCount(3)
})
