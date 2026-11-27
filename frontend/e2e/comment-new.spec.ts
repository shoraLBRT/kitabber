import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The comments new since the previous opening (E09-08, #105, FR-COMMENT, ADR-0109) in both profiles against the fake
// API (e2e/fake-api.mjs), which keeps the openings per client address: the first opening marks nothing, the next one
// marks the comments the fake names «новый» and «К следующему новому · N» goes through them in the order shown — on a
// wide screen also to «Седьмой уровень», read in place below the six levels shown; a phone, which opens a deep branch
// on its own screen, goes through the ones shown. A guest has no marks. The marks and the button's states are in
// tests/comments/NewComments.spec.ts; which comments are new by the server's rules, in CommentVisitApiTests.

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
// The comments of the titled post in the order of the fake: the seven levels of the first thread are 0xd00000 to 6.
const nth = (n: number) => `0195f3a0-0000-7000-8000-${(0xd00000 + n).toString(16).padStart(12, '0')}`
const seventh = nth(6)
const anotherReply = nth(9)
const fromChannel = nth(10)

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function signIn(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openPost(page: Page) {
  const opened = page.waitForResponse(
    (response) => response.url().endsWith('/bff/comment-visit') && response.ok(),
  )
  await page.goto(titled)
  await waitForHydration(page)
  await opened
  await expect(page.getByTestId('comment').first()).toBeVisible()
}

const commentOf = (page: Page, id: string) => page.locator(`[data-comment-id="${id}"]`)

test.describe('новые с прошлого визита', () => {
  test('второе открытие подсвечивает новые, кнопка ведёт по ним', async ({ page }) => {
    await signIn(page)
    await openPost(page)
    await expect(page.locator('[data-new="true"]')).toHaveCount(0)
    await expect(page.getByTestId('comments-next-new')).toHaveCount(0)

    await openPost(page)
    const fresh = isTouch() ? [anotherReply, fromChannel] : [anotherReply, fromChannel, seventh]
    const shown = fresh.slice(0, 2)
    await expect(page.locator('[data-new="true"]')).toHaveCount(shown.length)
    for (const id of shown) {
      await expect(commentOf(page, id)).toHaveAttribute('data-new', 'true')
      await expect(
        commentOf(page, id).locator(':scope > .comment__head').getByTestId('comment-new'),
      ).toHaveText('новый')
    }
    // The mark is not the highlight of a link.
    await expect(page.locator('[data-highlighted="true"]')).toHaveCount(0)
    await expectNoHorizontalScroll(page)

    const next = page.getByTestId('comments-next-new')
    for (const [at, id] of fresh.entries()) {
      await expect(next).toHaveText(`К следующему новому · ${fresh.length - at}`)
      await press(next)
      await expect(commentOf(page, id)).toHaveAttribute('data-new', 'true')
      await expect(commentOf(page, id).locator(':scope > .comment__head')).toBeInViewport()
    }
    await expect(next).toHaveCount(0)
    if (!isTouch()) {
      await expect(commentOf(page, seventh).getByTestId('comment-text').first()).toHaveText('Седьмой уровень')
    }
  })

  test('у гостя новых нет', async ({ page }) => {
    await page.goto(titled)
    await waitForHydration(page)
    await expect(page.getByTestId('comment').first()).toBeVisible()
    await expect(page.locator('[data-new="true"]')).toHaveCount(0)
    await expect(page.getByTestId('comments-next-new')).toHaveCount(0)
  })
})
