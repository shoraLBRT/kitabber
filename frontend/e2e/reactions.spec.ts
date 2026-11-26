import { randomUUID } from 'node:crypto'
import type { APIRequestContext, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The row of reactions with their counts (E10-03, FR-REACT, ADR-0072) in both profiles against the fake API
// (e2e/fake-api.mjs): the own reaction is marked in the row, and the counts of the post and of its comments follow the
// realtime event `reactions.changed` in the topic `post:<id>` without a reload, formatted by the locale, while the own
// mark stays. The order of the chips and the parsing of the event are in tests/reactions/reactions.spec.ts.

const api = 'http://127.0.0.1:3102'
const postId = '0195f3a0-0000-7000-8000-0000000000c3'
const inTopic = `/books/${postId}-knigi-oseni`
const titledId = '0195f3a0-0000-7000-8000-0000000000c1'
const titled = `/u/anna/${titledId}-privet-mir`

async function asOwnClient(page: Page): Promise<string> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  const client = randomUUID()
  await page.setExtraHTTPHeaders({
    'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}`,
    'x-e2e-client': client,
  })
  return client
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

/** Waits until the page listens to the post's topic, so a pushed event is not lost. */
async function subscribed(request: APIRequestContext, client: string, topic: string): Promise<void> {
  await expect
    .poll(async () => {
      const state = await (await request.get(`${api}/__realtime?client=${client}`)).json()
      return (state.connections as { open: boolean; topics: string[] }[]).some(
        (c) => c.open && c.topics.includes(topic),
      )
    })
    .toBe(true)
}

async function push(request: APIRequestContext, client: string, topic: string, data: unknown): Promise<void> {
  await request.post(`${api}/__realtime/push?client=${client}`, {
    data: { topic, type: 'reactions.changed', data },
  })
}

test.use({ actionTimeout: 10_000 })

test.describe('ряд реакций', () => {
  test('своя реакция выделена, счётчик поста меняется в реальном времени', async ({ page, request }) => {
    const client = await asOwnClient(page)
    await signIn(page)
    await page.goto(inTopic)
    await waitForHydration(page)
    const row = page
      .getByRole('toolbar', { name: 'Действия с постом' })
      .getByRole('group', { name: 'Реакции' })
    const like = row.getByRole('button', { name: 'Нравится' })
    await expect(like).toContainText('3')
    await press(like)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(like).toHaveClass(/reaction--mine/)
    await expect(like).toContainText('4')

    await subscribed(request, client, `post:${postId}`)
    await push(request, client, `post:${postId}`, {
      target: 'post',
      targetId: postId,
      reactions: [{ kind: 'like', count: 1000 }],
    })
    await expect(page.getByTestId('post-like-count')).toHaveText(/^1\s000$/)
    await expect(like).toHaveAttribute('aria-pressed', 'true')

    // An event of another post's topic changes nothing here.
    await push(request, client, `post:${titledId}`, {
      target: 'post',
      targetId: titledId,
      reactions: [{ kind: 'like', count: 7 }],
    })
    await push(request, client, `post:${postId}`, { target: 'post', targetId: postId, reactions: [] })
    await expect(page.getByTestId('post-like-count')).toHaveCount(0)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expectNoHorizontalScroll(page)
  })

  test('счётчик комментария меняется в реальном времени', async ({ page, request }) => {
    const client = await asOwnClient(page)
    await signIn(page)
    await page.goto(titled)
    await waitForHydration(page)
    const second = page
      .getByTestId('comments')
      .locator('[data-testid="comment"][aria-level="1"]')
      .filter({ hasText: 'Вторая ветка' })
      .first()
    const like = second.locator(':scope > .comment__actions').getByTestId('comment-like')
    await expect(like).toContainText('2')
    const commentId = await second.getAttribute('data-comment-id')
    expect(commentId).not.toBeNull()

    await subscribed(request, client, `post:${titledId}`)
    await push(request, client, `post:${titledId}`, {
      target: 'comment',
      targetId: commentId,
      reactions: [{ kind: 'like', count: 12 }],
    })
    await expect(like).toContainText('12')
    await expect(like).toHaveAttribute('aria-pressed', 'false')
    await expectNoHorizontalScroll(page)
  })
})
