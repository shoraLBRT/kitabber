import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import { appendPage, feedPostPath, loadFreshFeed } from '../../api/feeds'
import type { Post } from '../../api/posts'

// Reading the fresh feed page by page (E08-01, ADR-0043).
function clientAnswering(status: number, body: unknown) {
  const fetch = vi.fn(
    async () =>
      new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }),
  )
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

const post = (id: string) => ({ id, blogId: 'b1', slug: null }) as unknown as Post

describe('loadFreshFeed', () => {
  it('reads the first page without a cursor', async () => {
    const page = { items: [post('p1')], nextCursor: 'next' }
    const { client, fetch } = clientAnswering(200, page)
    expect(await loadFreshFeed(client)).toEqual({ status: 'found', page })
    expect((fetch.mock.calls[0] as unknown as [Request])[0].url).toBe('http://api.test/api/v1/feeds/fresh')
  })

  it('passes the cursor of the previous page back as it is', async () => {
    const { client, fetch } = clientAnswering(200, { items: [], nextCursor: null })
    await loadFreshFeed(client, 'eyJhIjoxfQ')
    expect((fetch.mock.calls[0] as unknown as [Request])[0].url).toBe(
      'http://api.test/api/v1/feeds/fresh?cursor=eyJhIjoxfQ',
    )
  })

  it('is an error when the API fails or cannot be reached', async () => {
    expect(await loadFreshFeed(clientAnswering(400, { code: 'invalid_cursor' }).client, 'x')).toEqual({
      status: 'error',
    })
    const unreachable = createApiClient({
      baseUrl: 'http://api.test',
      fetch: vi.fn(async () => {
        throw new TypeError('network')
      }),
    })
    expect(await loadFreshFeed(unreachable)).toEqual({ status: 'error' })
  })
})

describe('appendPage', () => {
  it('adds the next page after the posts shown and never shows a post twice', () => {
    const shown = [post('p1'), post('p2')]
    expect(appendPage(shown, { items: [post('p2'), post('p3')], nextCursor: null }).map((p) => p.id)).toEqual(
      ['p1', 'p2', 'p3'],
    )
  })
})

describe('feedPostPath', () => {
  it('leads through the blog id to the post, which redirects to its canonical address', () => {
    expect(feedPostPath({ id: 'p1', slug: 'privet', blogId: 'b1' })).toBe('/u/b1/p1-privet')
    expect(feedPostPath({ id: 'p1', slug: null, blogId: 'b1' })).toBe('/u/b1/p1')
  })
})
