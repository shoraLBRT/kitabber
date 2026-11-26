import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import {
  appendPage,
  feedEntryKey,
  feedPostPath,
  feedSnapshotLimit,
  loadBlogFeed,
  loadFreshFeed,
  rememberFeed,
  feedCommentPath,
  feedItemPath,
  feedPreview,
  type FeedPost,
  type FeedSnapshot,
} from '../../api/feeds'

// Reading the fresh feed page by page (E08-01, ADR-0043).
function clientAnswering(status: number, body: unknown) {
  const fetch = vi.fn(
    async () =>
      new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }),
  )
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

const post = (id: string) =>
  ({ post: { id, blogId: 'b1', slug: null }, author: null, topic: null }) as unknown as FeedPost

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

describe('loadBlogFeed', () => {
  it('reads a page of one blog or topic with the cursor of the previous page (E11-01)', async () => {
    const page = { items: [post('p1')], nextCursor: null }
    const { client, fetch } = clientAnswering(200, page)
    expect(await loadBlogFeed(client, 'b1', 'eyJhIjoxfQ')).toEqual({ status: 'found', page })
    expect((fetch.mock.calls[0] as unknown as [Request])[0].url).toBe(
      'http://api.test/api/v1/feeds/blogs/b1?cursor=eyJhIjoxfQ',
    )
  })

  it('is an error when the blog is gone or the API fails', async () => {
    const gone = clientAnswering(404, { code: 'posts.blog_not_found' }).client
    expect(await loadBlogFeed(gone, 'b1')).toEqual({ status: 'error' })
    expect(await loadBlogFeed(clientAnswering(500, {}).client, 'b1')).toEqual({ status: 'error' })
  })
})

describe('appendPage', () => {
  it('adds the next page after the posts shown and never shows a post twice', () => {
    const shown = [post('p1'), post('p2')]
    expect(
      appendPage(shown, { items: [post('p2'), post('p3')], nextCursor: null }).map((p) => p.post.id),
    ).toEqual(['p1', 'p2', 'p3'])
  })
})

describe('feedPostPath', () => {
  it('leads through the blog id to the post, which redirects to its canonical address', () => {
    expect(feedPostPath({ id: 'p1', slug: 'privet', blogId: 'b1' })).toBe('/u/b1/p1-privet')
    expect(feedPostPath({ id: 'p1', slug: null, blogId: 'b1' })).toBe('/u/b1/p1')
  })
})

describe('feedItemPath', () => {
  const author = { id: 'b1', type: 'personal', name: null, nickname: 'anna', avatarSources: [] }
  const topic = { id: 't1', type: 'topic', name: 'Книги', nickname: 'books', avatarSources: [] }
  const post = { id: 'p1', slug: 'privet', blogId: 'b1' } as unknown as FeedPost['post']

  it('leads to the post in its topic, else in the author blog it is in, else by the blog id', () => {
    expect(feedItemPath({ post: { ...post, blogId: 't1' }, author, topic })).toBe('/books/p1-privet')
    expect(feedItemPath({ post, author, topic: null })).toBe('/u/anna/p1-privet')
    expect(feedItemPath({ post: { ...post, blogId: 'b9' }, author, topic: null })).toBe('/u/b9/p1-privet')
    expect(feedItemPath({ post, author: null, topic: null })).toBe('/u/b1/p1-privet')
  })

  it('leads to a comment on the post page by its fragment (E08-15)', () => {
    expect(feedCommentPath({ post, author, topic: null }, 'c1')).toBe('/u/anna/p1-privet#comment-c1')
  })
})

// The cut of a longread on its card (E08-02, ADR-0052).
describe('feedPreview', () => {
  const paragraph = (text: string) => ({ type: 'paragraph', data: { text } })
  const document = (blocks: unknown[]) => ({ version: 1, blocks })

  it('gives a post of up to three short blocks whole', () => {
    const short = document([paragraph('Раз'), paragraph('Два'), paragraph('Три')])
    expect(feedPreview(short)).toEqual({ document: short, cut: false })
  })

  it('cuts a post after its third block', () => {
    const long = document([paragraph('1'), paragraph('2'), paragraph('3'), paragraph('4')])
    expect(feedPreview(long)).toEqual({ document: document(long.blocks.slice(0, 3)), cut: true })
  })

  it('cuts after the block whose text reaches 600 characters, but shows the first block always', () => {
    const big = 'а'.repeat(400)
    const long = document([paragraph(big), paragraph(big), paragraph('хвост')])
    expect(feedPreview(long).document).toEqual(document(long.blocks.slice(0, 2)))
    const huge = document([paragraph('б'.repeat(2000)), paragraph('хвост')])
    expect(feedPreview(huge)).toEqual({ document: document(huge.blocks.slice(0, 1)), cut: true })
  })

  it('counts the text people read, not the markup, file ids or addresses', () => {
    const marked = `<b>${'в'.repeat(250)}</b>`
    const image = {
      type: 'image',
      data: { file: { id: 'x'.repeat(900), url: 'y'.repeat(900) }, caption: '' },
    }
    const blocks = [paragraph(marked), image, paragraph(marked)]
    expect(feedPreview(document(blocks)).cut).toBe(false)
  })

  it('gives anything that is not a document as it is', () => {
    expect(feedPreview(null)).toEqual({ document: null, cut: false })
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
