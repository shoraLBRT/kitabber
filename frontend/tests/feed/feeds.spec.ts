import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import {
  appendPage,
  feedEntryKey,
  feedPostPath,
  feedSnapshotLimit,
  loadFreshFeed,
  rememberFeed,
  type FeedSnapshot,
} from '../../api/feeds'
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

// Coming back to a feed (E08-03, ADR-0051): a snapshot per history entry.
describe('rememberFeed', () => {
  const snapshot = (id: string): FeedSnapshot => ({ posts: [post(id)], cursor: null })

  it('keeps the newest snapshot of an entry', () => {
    const store = rememberFeed(rememberFeed({}, 'fresh:a', snapshot('p1')), 'fresh:a', snapshot('p2'))
    expect(store).toEqual({ 'fresh:a': snapshot('p2') })
  })

  it('forgets the entry seen longest ago beyond the limit', () => {
    let store: Record<string, FeedSnapshot> = {}
    for (let n = 0; n <= feedSnapshotLimit; n++) store = rememberFeed(store, `fresh:${n}`, snapshot(`p${n}`))
    expect(Object.keys(store)).toHaveLength(feedSnapshotLimit)
    expect(store['fresh:0']).toBeUndefined()
    expect(store[`fresh:${feedSnapshotLimit}`]).toEqual(snapshot(`p${feedSnapshotLimit}`))
  })
})

describe('feedEntryKey', () => {
  function fakeHistory(state: unknown) {
    const history = { state, replaceState: vi.fn((next: unknown) => (history.state = next)) }
    return history
  }

  it('names a new entry once and keeps the router fields', () => {
    const history = fakeHistory({ back: '/', current: '/new', position: 3 })
    expect(feedEntryKey(history, () => 'k1')).toBe('k1')
    expect(history.state).toEqual({ back: '/', current: '/new', position: 3, feedEntry: 'k1' })
    expect(feedEntryKey(history, () => 'k2')).toBe('k1')
    expect(history.replaceState).toHaveBeenCalledTimes(1)
  })

  it('names an entry without a state', () => {
    const history = fakeHistory(null)
    expect(feedEntryKey(history, () => 'k1')).toBe('k1')
    expect(history.state).toEqual({ feedEntry: 'k1' })
  })

  it('makes different keys for different entries by default', () => {
    expect(feedEntryKey(fakeHistory({}))).not.toBe(feedEntryKey(fakeHistory({})))
  })
})
