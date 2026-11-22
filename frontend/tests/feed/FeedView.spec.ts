import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import FeedView from '../../components/feed/FeedView.vue'
import type { FeedState } from '../../api/feeds'
import type { Post } from '../../api/posts'

// The states of a feed (E08-01, docs/ui/feed.md); the server render and the pages against the fake API are in
// e2e/feed.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

function post(id: string, title: string | null, text: string): Post {
  return {
    id,
    blogId: '0195f3a0-0000-7000-8000-0000000000b1',
    authorAccountId: '0195f3a0-0000-7000-8000-0000000000a1',
    title,
    slug: title ? 'zagolovok' : null,
    body: {
      version: 1,
      blocks: [{ id: 'b1', type: 'paragraph', data: { text } }],
    } as unknown as Post['body'],
    status: 'published',
    origin: 'native',
    source: null,
    createdAt: '2026-11-20T10:00:00Z',
    updatedAt: '2026-11-20T10:00:00Z',
    publishedAt: '2026-11-20T10:05:00Z',
    images: {},
  }
}

const titled = post('0195f3a0-0000-7000-8000-0000000000c1', 'Заголовок', 'Первый <b>абзац</b>.')
const short = post('0195f3a0-0000-7000-8000-0000000000c2', null, 'Короткий пост.')

function mountView(state: FeedState, extra: { loadingMore?: boolean; moreFailed?: boolean } = {}) {
  return mount(FeedView, { props: { title: 'Свежее', state, ...extra }, global: { stubs } })
}

// A stand-in for IntersectionObserver: the test says when the mark at the end of the list comes into view.
class FakeObserver {
  observed: Element[] = []
  disconnected = false
  constructor(
    private readonly callback: IntersectionObserverCallback,
    readonly options?: IntersectionObserverInit,
  ) {
    observers.push(this)
  }
  observe(element: Element) {
    this.observed.push(element)
  }
  unobserve(element: Element) {
    this.observed = this.observed.filter((observed) => observed !== element)
  }
  disconnect() {
    this.disconnected = true
    this.observed = []
  }
  fire(isIntersecting: boolean) {
    const entries = this.observed.map((target) => ({ target, isIntersecting }) as IntersectionObserverEntry)
    this.callback(entries, this as unknown as IntersectionObserver)
  }
}
let observers: FakeObserver[] = []

beforeEach(() => {
  observers = []
  vi.stubGlobal('IntersectionObserver', FakeObserver)
})

afterEach(() => vi.unstubAllGlobals())

describe('FeedView', () => {
  it('shows the posts in the given order, each leading to its post', () => {
    const wrapper = mountView({ status: 'found', posts: [titled, short], hasMore: false })
    expect(wrapper.get('h1').text()).toBe('Свежее')
    const items = wrapper.findAll('[data-testid="feed-item"]')
    expect(items).toHaveLength(2)
    expect(items[0]!.get('h2').text()).toBe('Заголовок')
    expect(items[0]!.text()).toContain('Первый абзац.')
    expect(items[0]!.get('a').attributes('href')).toBe(`/u/${titled.blogId}/${titled.id}-zagolovok`)
    expect(items[0]!.get('time').attributes('datetime')).toBe('2026-11-20T10:05:00Z')
    expect(items[0]!.get('time').text()).toMatch(/^20 ноября\D+10:05$/)
    // A short post without a title is headed by its first line and opens by its id.
    expect(items[1]!.get('h2').text()).toBe('Короткий пост.')
    expect(items[1]!.get('a').attributes('href')).toBe(`/u/${short.blogId}/${short.id}`)
    expect(wrapper.find('[data-testid="feed-more"]').exists()).toBe(false)
  })

  it('asks for the next page when the end of the list comes near, and again for a list still too short', async () => {
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true })
    const observer = observers.at(-1)!
    expect(observer.options?.rootMargin).toBe('0px 0px 800px 0px')
    const mark = wrapper.get('[data-testid="feed-sentinel"]').element
    expect(observer.observed).toEqual([mark])
    observer.fire(false)
    expect(wrapper.emitted('more')).toBeUndefined()
    observer.fire(true)
    expect(wrapper.emitted('more')).toHaveLength(1)

    // While the page loads the mark is gone; after it a new mark is watched, so a short list asks once more.
    await wrapper.setProps({ loadingMore: true })
    expect(wrapper.find('[data-testid="feed-sentinel"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="feed-loading-more"]').attributes('role')).toBe('status')
    expect(observer.observed).toEqual([])
    await wrapper.setProps({
      loadingMore: false,
      state: { status: 'found', posts: [titled, short], hasMore: true },
    })
    expect(observer.observed).toEqual([wrapper.get('[data-testid="feed-sentinel"]').element])
  })

  it('stops watching at the end of the feed and when it leaves the page', async () => {
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true })
    const observer = observers.at(-1)!
    await wrapper.setProps({ state: { status: 'found', posts: [titled, short], hasMore: false } })
    expect(wrapper.find('[data-testid="feed-sentinel"]').exists()).toBe(false)
    expect(observer.observed).toEqual([])
    wrapper.unmount()
    expect(observer.disconnected).toBe(true)
  })

  it('keeps the posts when the next page fails and offers to try again instead of trying on its own', async () => {
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true }, { moreFailed: true })
    expect(wrapper.findAll('[data-testid="feed-item"]')).toHaveLength(1)
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось загрузить следующие посты')
    expect(wrapper.find('[data-testid="feed-sentinel"]').exists()).toBe(false)
    const retry = wrapper.get('[data-testid="feed-more"]')
    expect(retry.text()).toBe('Повторить')
    await retry.trigger('click')
    expect(wrapper.emitted('more')).toHaveLength(1)
  })

  it('disables the retry while the next page loads', () => {
    const wrapper = mountView(
      { status: 'found', posts: [titled], hasMore: true },
      { loadingMore: true, moreFailed: true },
    )
    expect(wrapper.get('[data-testid="feed-more"]').attributes('disabled')).toBeDefined()
  })

  it('asks for the next page by a button where the browser cannot watch the list', async () => {
    vi.stubGlobal('IntersectionObserver', undefined)
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true })
    await nextTick()
    expect(wrapper.find('[data-testid="feed-sentinel"]').exists()).toBe(false)
    await wrapper.get('[data-testid="feed-more"]').trigger('click')
    expect(wrapper.emitted('more')).toHaveLength(1)
  })

  it('shows the loading state', () => {
    const wrapper = mountView({ status: 'loading' })
    expect(wrapper.get('[data-testid="feed-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[role="status"]').text()).toBe('Загрузка ленты')
  })

  it('shows the empty state', () => {
    const wrapper = mountView({ status: 'found', posts: [], hasMore: false })
    expect(wrapper.get('[data-testid="feed-empty"]').text()).toBe('Здесь пока нет постов.')
    expect(wrapper.find('[data-testid="feed-list"]').exists()).toBe(false)
  })

  it('shows a failure with a retry', async () => {
    const wrapper = mountView({ status: 'error' })
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось загрузить ленту')
    await wrapper.get('[data-testid="feed-error"] button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})
