import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
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
  }
}

const titled = post('0195f3a0-0000-7000-8000-0000000000c1', 'Заголовок', 'Первый <b>абзац</b>.')
const short = post('0195f3a0-0000-7000-8000-0000000000c2', null, 'Короткий пост.')

function mountView(state: FeedState, extra: { loadingMore?: boolean; moreFailed?: boolean } = {}) {
  return mount(FeedView, { props: { title: 'Свежее', state, ...extra }, global: { stubs } })
}

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

  it('asks for the next page while there is one', async () => {
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true })
    await wrapper.get('[data-testid="feed-more"]').trigger('click')
    expect(wrapper.emitted('more')).toHaveLength(1)
  })

  it('keeps the posts when the next page fails and offers to try again', () => {
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true }, { moreFailed: true })
    expect(wrapper.findAll('[data-testid="feed-item"]')).toHaveLength(1)
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось загрузить следующие посты')
    expect(wrapper.get('[data-testid="feed-more"]').text()).toBe('Повторить')
  })

  it('disables the button while the next page loads', () => {
    const wrapper = mountView({ status: 'found', posts: [titled], hasMore: true }, { loadingMore: true })
    expect(wrapper.get('[data-testid="feed-more"]').attributes('disabled')).toBeDefined()
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
