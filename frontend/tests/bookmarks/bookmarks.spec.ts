import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { createApiClient } from '../../api/client'
import {
  changeBookmark,
  feedBookmarks,
  loadFeedBookmarks,
  noBookmark,
  postBookmarks,
  toggled,
  type BookmarkState,
  type BookmarkTarget,
} from '../../api/bookmarks'
import type { Comment } from '../../api/comments'
import type { FeedPost } from '../../api/feeds'
import type { Post } from '../../api/posts'
import BookmarkButton from '../../components/bookmarks/BookmarkButton.vue'
import FeedCard from '../../components/feed/FeedCard.vue'
import PostActions from '../../components/post/PostActions.vue'
import { bookmarkAction, useBookmarks, type Bookmarks } from '../../composables/useBookmarks'
import { commentMenuItems } from '../../utils/comment-menu'

// Bookmarks on posts and comments (E10-07, ADR-0113) without a page: the API's answers, the state shown before the
// server answers, the button on the action panel and on a feed card, and the comment menu's item. The pages against
// the fake API are in e2e/bookmarks.spec.ts.

const postId = '0195f3a0-0000-7000-8000-0000000000c1'

function clientAnswering(answer: { status: number; body: unknown }) {
  const fetch = vi.fn(
    async () =>
      new Response(JSON.stringify(answer.body), {
        status: answer.status,
        headers: { 'content-type': 'application/json' },
      }),
  )
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

/** A stand-in for useBookmarks: the states given, every press recorded. */
function fakeBookmarks(states: Record<string, BookmarkState> = {}, busy = false) {
  const presses: [BookmarkTarget, string][] = []
  const bookmarks = {
    states: ref(states),
    of: (target: BookmarkTarget, id: string) => states[`${target}:${id}`] ?? noBookmark,
    isBusy: () => busy,
    toggle: (target: BookmarkTarget, id: string) => presses.push([target, id]),
    loadPost: async () => {},
    loadPosts: async () => {},
  } as unknown as Bookmarks
  return { bookmarks, presses }
}

describe('bookmarks API', () => {
  it('puts a bookmark with PUT and takes it out with DELETE on the target', async () => {
    const { client, fetch } = clientAnswering({ status: 200, body: { bookmarked: true, count: null } })
    expect(await changeBookmark(client, 'post', postId, true)).toEqual({
      status: 'done',
      state: { bookmarked: true, count: null },
    })
    await changeBookmark(client, 'comment', 'c1', false)
    const [put, del] = fetch.mock.calls.map((call) => (call as unknown as [Request])[0])
    expect([put!.method, new URL(put!.url).pathname]).toEqual(['PUT', `/api/v1/posts/${postId}/bookmark`])
    expect([del!.method, new URL(del!.url).pathname]).toEqual(['DELETE', '/api/v1/comments/c1/bookmark'])
  })

  it('says why a change did not go through', async () => {
    expect(
      await changeBookmark(clientAnswering({ status: 401, body: {} }).client, 'post', postId, true),
    ).toEqual({
      status: 'unauthorized',
    })
    expect(
      await changeBookmark(clientAnswering({ status: 404, body: {} }).client, 'post', postId, true),
    ).toEqual({
      status: 'refused',
      message: 'Пост или комментарий не найден: возможно, его удалили.',
    })
    expect(
      await changeBookmark(clientAnswering({ status: 500, body: {} }).client, 'post', postId, true),
    ).toEqual({
      status: 'error',
    })
  })

  it('asks about the posts of a feed in one request', async () => {
    const { client, fetch } = clientAnswering({ status: 200, body: { posts: [] } })
    await loadFeedBookmarks(client, ['a', 'b'])
    const request = (fetch.mock.calls[0] as unknown as [Request])[0]
    expect(new URL(request.url).searchParams.getAll('ids')).toEqual(['a', 'b'])
    expect(await loadFeedBookmarks(clientAnswering({ status: 400, body: {} }).client, ['x'])).toBeNull()
  })

  it('keys the states of a post page and of a feed by target', () => {
    expect(
      postBookmarks({
        postId,
        post: { bookmarked: true, count: 3 },
        comments: [{ id: 'c1', bookmarked: false, count: 2 }],
      }),
    ).toEqual({
      [`post:${postId}`]: { bookmarked: true, count: 3 },
      'comment:c1': { bookmarked: false, count: 2 },
    })
    // Every asked post gets a state, an unlisted one as not kept.
    expect(feedBookmarks(['a', 'b'], { posts: [{ id: 'a', bookmarked: true, count: null }] })).toEqual({
      'post:a': { bookmarked: true, count: null },
      'post:b': noBookmark,
    })
  })

  it('flips the mark at once and moves the author’s count with it', () => {
    expect(toggled({ bookmarked: false, count: null })).toEqual({ bookmarked: true, count: null })
    expect(toggled({ bookmarked: false, count: 4 })).toEqual({ bookmarked: true, count: 5 })
    expect(toggled({ bookmarked: true, count: 1 })).toEqual({ bookmarked: false, count: 0 })
  })
})

describe('BookmarkButton', () => {
  it('is pressed while the post is kept, and shows the count only when there is one', () => {
    const off = mount(BookmarkButton, { props: { state: noBookmark } })
    expect(off.attributes('aria-pressed')).toBe('false')
    expect(off.text()).toContain('В закладки')
    expect(off.find('[data-testid="bookmark-count"]').exists()).toBe(false)

    const on = mount(BookmarkButton, { props: { state: { bookmarked: true, count: 3 }, busy: true } })
    expect(on.attributes('aria-pressed')).toBe('true')
    expect(on.attributes('aria-busy')).toBe('true')
    expect(on.attributes('title')).toBe('Убрать из закладок')
    expect(on.get('[data-testid="bookmark-count"]').text()).toBe('3')
    expect(on.text()).toContain('В закладках у 3 человек')

    const none = mount(BookmarkButton, { props: { state: { bookmarked: false, count: 0 } } })
    expect(none.find('[data-testid="bookmark-count"]').exists()).toBe(false)
  })

  it('passes a press on', async () => {
    const button = mount(BookmarkButton, { props: { state: noBookmark } })
    await button.trigger('click')
    expect(button.emitted('press')).toHaveLength(1)
  })
})

describe('PostActions', () => {
  it('offers «В закладки» on a published post and passes the press to the bookmarks', async () => {
    const { bookmarks, presses } = fakeBookmarks({ [`post:${postId}`]: { bookmarked: true, count: 2 } })
    const panel = mount(PostActions, { props: { path: '/u/anna/x', postId, own: true, bookmarks } })
    const button = panel.get('[data-testid="post-bookmark"]')
    expect(button.attributes('aria-pressed')).toBe('true')
    expect(button.get('[data-testid="bookmark-count"]').text()).toBe('2')
    await button.trigger('click')
    expect(presses).toEqual([['post', postId]])

    expect(
      mount(PostActions, { props: { path: '/u/anna/x', postId: null, bookmarks } })
        .find('[data-testid="post-bookmark"]')
        .exists(),
    ).toBe(false)
    expect(
      mount(PostActions, { props: { path: '/u/anna/x', postId } })
        .find('[data-testid="post-bookmark"]')
        .exists(),
    ).toBe(false)
  })
})

describe('FeedCard', () => {
  const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }
  const item = {
    post: {
      id: postId,
      blogId: 'b1',
      authorAccountId: 'a1',
      title: 'Заголовок',
      slug: 'zagolovok',
      body: { version: 1, blocks: [{ id: 'b1', type: 'paragraph', data: { text: 'Текст.' } }] },
      status: 'published',
      origin: 'native',
      source: null,
      createdAt: '2026-11-20T10:00:00Z',
      updatedAt: '2026-11-20T10:00:00Z',
      publishedAt: '2026-11-20T10:05:00Z',
      images: {},
      imagePreviews: {},
      viewCount: 0,
    } as unknown as Post,
    author: null,
    topic: null,
    bestComment: null,
  } as FeedPost

  it('has «В закладки» among the counters', async () => {
    const { bookmarks, presses } = fakeBookmarks()
    const card = mount(FeedCard, { props: { item, bookmarks }, global: { stubs } })
    const button = card.get('[data-testid="feed-card-bookmark"]')
    expect(button.attributes('aria-pressed')).toBe('false')
    await button.trigger('click')
    expect(presses).toEqual([['post', postId]])

    expect(
      mount(FeedCard, { props: { item }, global: { stubs } })
        .find('[data-testid="feed-card-bookmark"]')
        .exists(),
    ).toBe(false)
  })
})

describe('the comment menu', () => {
  const comment = {
    id: 'c1',
    postId,
    parentId: null,
    kind: 'native',
    authorAccountId: 'a2',
    authorBlocked: false,
    guest: null,
    text: 'Комментарий',
    deleted: false,
    createdAt: '2026-11-20T10:00:00Z',
    editedAt: null,
    editableUntil: null,
  } as unknown as Comment

  it('offers «В закладки» or «Убрать из закладок», with the count for the author', () => {
    const labels = (bookmark: BookmarkState | null, own = false) =>
      commentMenuItems(comment, own, 0, false, true, bookmark).map((item) => item.label)
    expect(labels(noBookmark)).toEqual(['Скопировать ссылку', 'В закладки'])
    expect(labels({ bookmarked: true, count: null })).toEqual(['Скопировать ссылку', 'Убрать из закладок'])
    expect(labels({ bookmarked: false, count: 2 }, true)).toEqual([
      'Скопировать ссылку',
      'В закладки · 2',
      'Удалить',
    ])
    expect(labels(null)).toEqual(['Скопировать ссылку'])
    expect(commentMenuItems({ ...comment, deleted: true }, false, 0, false, true, noBookmark)).toEqual([])
  })
})

describe('useBookmarks', () => {
  const shown: [string, string | undefined][] = []
  let signedIn = true
  let fetchMock: ReturnType<typeof vi.fn>
  let windowOpened: string | null = null

  beforeEach(() => {
    shown.length = 0
    signedIn = true
    windowOpened = null
    fetchMock = vi.fn()
    vi.stubGlobal('useAccount', () => ref({ signedIn }))
    vi.stubGlobal('useRequireAccount', () => (action: string, run: () => void) => {
      if (signedIn) run()
      else windowOpened = action
    })
    vi.stubGlobal('useRoute', () => ({ fullPath: '/new' }))
    vi.stubGlobal('useToast', () => ({
      show: (message: string, options?: { tone?: string }) => shown.push([message, options?.tone]),
    }))
    vi.stubGlobal('$fetch', fetchMock)
  })

  afterEach(() => vi.unstubAllGlobals())

  function withBookmarks(): Bookmarks {
    let found: Bookmarks | null = null
    mount(
      defineComponent({
        setup() {
          found = useBookmarks()
          return () => h('div')
        },
      }),
    )
    return found!
  }

  it('reads the feed’s posts once and shows a press at once, then the server’s answer', async () => {
    fetchMock.mockImplementation(async (url: string) => {
      if (url === '/bff/bookmarks') return { posts: [{ id: 'p1', bookmarked: true, count: null }] }
      return { status: 'done', state: { bookmarked: false, count: null } }
    })
    const bookmarks = withBookmarks()
    await bookmarks.loadPosts(['p1', 'p2'])
    await bookmarks.loadPosts(['p1', 'p2'])
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(bookmarks.of('post', 'p1')).toEqual({ bookmarked: true, count: null })
    expect(bookmarks.of('post', 'p2')).toEqual(noBookmark)

    bookmarks.toggle('post', 'p1')
    expect(bookmarks.of('post', 'p1').bookmarked).toBe(false)
    expect(bookmarks.isBusy('post', 'p1')).toBe(true)
    await flushPromises()
    expect(fetchMock).toHaveBeenLastCalledWith('/bff/bookmark', {
      method: 'POST',
      body: { target: 'post', id: 'p1', on: false },
    })
    expect(bookmarks.isBusy('post', 'p1')).toBe(false)
    expect(shown).toEqual([['Убрано из закладок', 'success']])
  })

  it('puts the old state back with a toast when the change fails', async () => {
    fetchMock.mockResolvedValue({ status: 'error' })
    const bookmarks = withBookmarks()
    bookmarks.toggle('comment', 'c1')
    expect(bookmarks.of('comment', 'c1').bookmarked).toBe(true)
    await flushPromises()
    expect(bookmarks.of('comment', 'c1')).toEqual(noBookmark)
    expect(shown).toEqual([['Не удалось изменить закладку. Попробуйте ещё раз.', 'error']])
  })

  it('reads nothing for a guest, whose press opens the sign-in window', async () => {
    signedIn = false
    const bookmarks = withBookmarks()
    await bookmarks.loadPosts(['p1'])
    await bookmarks.loadPost(postId)
    bookmarks.toggle('post', 'p1')
    expect(fetchMock).not.toHaveBeenCalled()
    expect(windowOpened).toBe(bookmarkAction('post', 'p1'))
  })
})
