import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import NewPostsBanner from '../../components/feed/NewPostsBanner.vue'
import { newPostsInterval, useNewPosts } from '../../composables/useNewPosts'
import {
  newerOverflow,
  newerPosts,
  showNewer,
  type FeedLookup,
  type FeedPage,
  type FeedPost,
} from '../../api/feeds'

// «Показать новые» (E08-08, ADR-0108): which posts are new, what the feed shows after the banner is pressed, the banner
// and the checks. The page against the fake API, in both profiles, is in e2e/new-posts.spec.ts.
const post = (id: string, publishedAt: string) =>
  ({ post: { id, blogId: 'b1', slug: null, publishedAt }, author: null, topic: null }) as unknown as FeedPost

const older = post('p1', '2026-11-20T10:00:00Z')
const oldest = post('p0', '2026-11-19T10:00:00Z')
const newer = post('p2', '2026-11-21T10:00:00Z')
const newest = post('p3', '2026-11-22T10:00:00Z')
const page = (items: FeedPost[], nextCursor: string | null = null): FeedPage => ({ items, nextCursor })

describe('newerPosts', () => {
  it('counts the posts above the newest one shown', () => {
    expect(newerPosts([older, oldest], page([newest, newer, older]))).toEqual([newest, newer])
  })

  it('finds nothing new when the first page starts with the post on top', () => {
    expect(newerPosts([older, oldest], page([older, oldest]))).toEqual([])
  })

  it('orders posts of the same moment by id, as the feed does', () => {
    const twin = post('p9', older.post.publishedAt!)
    expect(newerPosts([older], page([twin, older]))).toEqual([twin])
  })

  it('takes every post of the page as new to an empty feed', () => {
    expect(newerPosts([], page([newer]))).toEqual([newer])
  })

  it('never counts a post already shown or an older one that came back', () => {
    expect(newerPosts([newer, older], page([newer, oldest]))).toEqual([])
  })
})

describe('showNewer', () => {
  it('puts the new posts on top and keeps the cursor of the feed', () => {
    const shown = { posts: [older, oldest], cursor: 'c2' }
    expect(showNewer(shown, page([newest, newer, older], 'c1'))).toEqual({
      posts: [newest, newer, older, oldest],
      cursor: 'c2',
    })
  })

  it('starts the feed again from the first page when all of it is new', () => {
    const shown = { posts: [older, oldest], cursor: 'c2' }
    const first = page([newest, newer], 'c1')
    expect(newerOverflow(shown.posts, first)).toBe(true)
    expect(showNewer(shown, first)).toEqual({ posts: [newest, newer], cursor: 'c1' })
  })

  it('puts the whole page on top when it is the whole feed', () => {
    expect(newerOverflow([older], page([newer]))).toBe(false)
    expect(showNewer({ posts: [older], cursor: null }, page([newer]))).toEqual({
      posts: [newer, older],
      cursor: null,
    })
  })
})

describe('NewPostsBanner', () => {
  it('keeps its place empty while there is nothing new', () => {
    const wrapper = mount(NewPostsBanner, { props: { count: 0 } })
    expect(wrapper.find('button').exists()).toBe(false)
    expect(wrapper.get('[data-testid="new-posts"]').attributes('aria-live')).toBe('polite')
  })

  it('says how many posts are new and asks to show them', async () => {
    const wrapper = mount(NewPostsBanner, { props: { count: 3 } })
    const button = wrapper.get('button')
    expect(button.text()).toContain('Показать новые')
    expect(button.text()).toContain('3 новых поста')
    await button.trigger('click')
    expect(wrapper.emitted('show')).toHaveLength(1)
  })

  it('says there may be more when the whole page is new', () => {
    const wrapper = mount(NewPostsBanner, { props: { count: 20, more: true } })
    expect(wrapper.get('.new-posts__count').text()).toBe('20+')
    expect(wrapper.text()).toContain('новых постов: 20 и больше')
  })
})

describe('useNewPosts', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  function setup(answers: FeedLookup[]) {
    const posts = ref<FeedPost[] | null>([older, oldest])
    const cursor = ref<string | null>('c2')
    const load = vi.fn(async () => answers.shift() ?? { status: 'error' as const })
    let api!: ReturnType<typeof useNewPosts>
    const wrapper = mount(
      defineComponent({
        setup() {
          api = useNewPosts(posts, cursor, load)
          return () => h('div')
        },
      }),
    )
    return { posts, cursor, load, api: () => api, wrapper }
  }

  it('checks the first page on its interval and never puts new posts into the feed', async () => {
    const { posts, load, api } = setup([{ status: 'found', page: page([newer, older]) }])
    await vi.advanceTimersByTimeAsync(newPostsInterval - 1)
    expect(load).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(1)
    expect(load).toHaveBeenCalledTimes(1)
    expect(api().count.value).toBe(1)
    expect(posts.value).toEqual([older, oldest])
  })

  it('shows the new posts on top and scrolls up when asked', async () => {
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
    const { posts, cursor, api } = setup([{ status: 'found', page: page([newest, newer, older], 'c1') }])
    await api().check()
    api().show()
    expect(posts.value).toEqual([newest, newer, older, oldest])
    expect(cursor.value).toBe('c2')
    expect(api().count.value).toBe(0)
    expect(scroll).toHaveBeenCalledWith({ top: 0 })
  })

  it('stays quiet when a check fails and drops a check answered after a reset', async () => {
    let answer!: (lookup: FeedLookup) => void
    const { load, api } = setup([{ status: 'error' }])
    await api().check()
    expect(api().count.value).toBe(0)

    load.mockImplementationOnce(() => new Promise<FeedLookup>((resolve) => (answer = resolve)))
    const pending = api().check()
    api().reset()
    answer({ status: 'found', page: page([newer, older]) })
    await pending
    expect(api().count.value).toBe(0)
  })

  it('checks again when the tab comes back into view and stops when the feed is gone', async () => {
    const { load, wrapper } = setup([{ status: 'found', page: page([older]) }])
    document.dispatchEvent(new Event('visibilitychange'))
    await nextTick()
    expect(load).toHaveBeenCalledTimes(1)
    wrapper.unmount()
    await vi.advanceTimersByTimeAsync(newPostsInterval * 2)
    expect(load).toHaveBeenCalledTimes(1)
  })
})
