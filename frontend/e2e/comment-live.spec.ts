import { randomUUID } from 'node:crypto'
import type { APIRequestContext, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The comments others write while a post is open (E09-09, #106, FR-COMMENT, ADR-0112) in both profiles against the
// fake API (e2e/fake-api.mjs): the realtime event `comments.posted` in the topic `post:<id>` brings «N новых» and the
// heading counts the comment at once, while the tree does not move; the comment of an author the reader blocked and
// the event of another post count for nothing. «N новых» puts them in the tree marked «новый»: a reply under its
// parent, a reply deeper than the tree shows counted where the tree stops. The button's states and where a comment
// goes are in tests/comments/LiveComments.spec.ts; the event itself in CommentRealtimeApiTests.

const api = 'http://127.0.0.1:3102'
const postId = '0195f3a0-0000-7000-8000-0000000000c1'
const titled = `/u/anna/${postId}-privet-mir`
const otherPostId = '0195f3a0-0000-7000-8000-0000000000c3'
// The comments of the titled post in the order of the fake: the seven levels of the first thread are 0xd00000 to 6.
const nth = (n: number) => `0195f3a0-0000-7000-8000-${(0xd00000 + n).toString(16).padStart(12, '0')}`
const seventh = nth(6)
const secondThread = nth(7)

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

/** Someone else writes a comment (the fake keeps it) and the hub tells the page. */
async function written(
  request: APIRequestContext,
  client: string,
  input: { postId?: string; parentId?: string | null; text: string; author?: 'anna' | 'boris' },
): Promise<string> {
  const post = input.postId ?? postId
  const response = await request.post(`${api}/__comment-arrival`, {
    data: {
      postId: post,
      parentId: input.parentId ?? null,
      text: input.text,
      author: input.author ?? 'anna',
    },
  })
  const { id } = (await response.json()) as { id: string }
  await request.post(`${api}/__realtime/push?client=${client}`, {
    data: {
      topic: `post:${post}`,
      type: 'comments.posted',
      data: { commentId: id, postId: post, parentId: input.parentId ?? null },
    },
  })
  return id
}

test.use({ actionTimeout: 10_000 })

test.describe('комментарии в реальном времени', () => {
  test('новые ждут за «N новых», счётчик растёт, прочитанное не сдвигается', async ({ page, request }) => {
    const client = await asOwnClient(page)
    await signIn(page)
    await page.goto(titled)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    const count = comments.getByTestId('comments-count')
    await expect(comments.getByTestId('comment').first()).toBeVisible()
    const before = Number(((await count.textContent()) ?? '').replace(/\D/g, ''))
    expect(before).toBeGreaterThan(0)
    await subscribed(request, client, `post:${postId}`)

    // Reading the second thread: it stays where it is while the comments come.
    const reading = comments.locator(`[data-comment-id="${secondThread}"]`)
    await reading.scrollIntoViewIfNeeded()
    const top = (await reading.boundingBox())!.y

    const root = await written(request, client, { text: 'Живой комментарий' })
    const button = comments.getByTestId('comments-arrived')
    await expect(button).toHaveText('1 новый')
    await expect(count).toHaveText(String(before + 1))
    const reply = await written(request, client, { parentId: secondThread, text: 'Живой ответ' })
    const deep = await written(request, client, { parentId: seventh, text: 'Живой ответ на седьмой' })
    await expect(button).toHaveText('3 новых')
    await expect(button).toHaveAttribute('aria-label', 'Показать 3 новых комментария')
    await expect(count).toHaveText(String(before + 3))

    // Борис is on the reader's black list, and an event of another post is not about this one.
    await written(request, client, { text: 'От заблокированного', author: 'boris' })
    await written(request, client, { postId: otherPostId, text: 'Под другим постом' })
    // A comment by someone else after them counts; the two left out never join the tree below either.
    await written(request, client, { parentId: root, text: 'Ответ на живой' })
    await expect(button).toHaveText('4 новых')
    await expect(count).toHaveText(String(before + 4))
    await expect(comments.getByText('Живой комментарий')).toHaveCount(0)
    expect(Math.abs((await reading.boundingBox())!.y - top)).toBeLessThan(1)

    await press(button)
    await expect(button).toHaveCount(0)
    await expect(count).toHaveText(String(before + 4))
    for (const id of [root, reply]) {
      await expect(comments.locator(`[data-comment-id="${id}"]`)).toHaveAttribute('data-new', 'true')
    }
    await expect(comments.getByText('Ответ на живой')).toHaveCount(1)
    await expect(comments.getByText('От заблокированного')).toHaveCount(0)
    // The reply below the levels shown waits where the tree stops: one more to read there.
    await expect(comments.locator(`[data-comment-id="${deep}"]`)).toHaveCount(0)
    if (isTouch()) {
      await expect(comments.getByTestId('comment-open-branch').first()).toHaveText('Открыть ветку (5) →')
    } else {
      await expect(comments.getByTestId('comment-more').first()).toHaveText('Показать ещё 2 ответа')
    }
    await expectNoHorizontalScroll(page)
  })
})
