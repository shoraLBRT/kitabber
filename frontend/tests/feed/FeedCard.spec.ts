import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import FeedCard from '../../components/feed/FeedCard.vue'
import type { FeedBlog, FeedPost } from '../../api/feeds'
import type { Post } from '../../api/posts'

// The card of a post in a feed (E08-02, FR-FEED, ADR-0052): the author, the topic, the date, the title and the blocks;
// a short post whole, a longread cut with «Показать полностью». Both profiles against the fake API: e2e/feed.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const anna: FeedBlog = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  avatarSources: [],
}
const books: FeedBlog = {
  id: '0195f3a0-0000-7000-8000-0000000000b4',
  type: 'topic',
  name: 'Книги',
  nickname: 'books',
  avatarSources: [],
}

function paragraphs(count: number, text = 'Абзац'): unknown[] {
  return Array.from({ length: count }, (_, n) => ({
    id: `b${n}`,
    type: 'paragraph',
    data: { text: `${text} ${n + 1}.` },
  }))
}

function item(
  blocks: unknown[],
  extra: Partial<FeedPost> = {},
  title: string | null = 'Заголовок',
): FeedPost {
  const post: Post = {
    id: '0195f3a0-0000-7000-8000-0000000000c1',
    blogId: anna.id,
    authorAccountId: '0195f3a0-0000-7000-8000-000000000001',
    title,
    slug: title ? 'zagolovok' : null,
    body: { version: 1, blocks } as unknown as Post['body'],
    status: 'published',
    origin: 'native',
    source: null,
    createdAt: '2026-11-20T10:00:00Z',
    updatedAt: '2026-11-20T10:00:00Z',
    publishedAt: '2026-11-20T10:05:00Z',
    images: {},
    imagePreviews: {},
  }
  return { post, author: anna, topic: null, bestComment: null, ...extra }
}

const mountCard = (feedItem: FeedPost) => mount(FeedCard, { props: { item: feedItem }, global: { stubs } })

describe('FeedCard', () => {
  it('shows the author, the date and the title, leading to the canonical address of the post', () => {
    const wrapper = mountCard(item(paragraphs(1)))
    expect(wrapper.get('[data-testid="feed-card-author"] .feed-card__name').text()).toBe('Анна')
    expect(wrapper.get('[data-testid="feed-card-author"]').attributes('href')).toBe('/u/anna')
    expect(wrapper.find('[data-testid="feed-card-topic"]').exists()).toBe(false)
    expect(wrapper.get('time').attributes('datetime')).toBe('2026-11-20T10:05:00Z')
    expect(wrapper.get('time').text()).toMatch(/^20 ноября\D+10:05$/)
    expect(wrapper.get('h2').text()).toBe('Заголовок')
    expect(wrapper.get('[data-testid="feed-card-title"]').attributes('href')).toBe(
      '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-zagolovok',
    )
  })

  it('shows the topic of a post in a topic and leads to the post there', () => {
    const wrapper = mountCard(item(paragraphs(1), { topic: books }))
    expect(wrapper.get('[data-testid="feed-card-topic"]').text()).toBe('Книги')
    expect(wrapper.get('[data-testid="feed-card-topic"]').attributes('href')).toBe('/books')
    expect(wrapper.get('[data-testid="feed-card-date"]').attributes('href')).toBe(
      '/books/0195f3a0-0000-7000-8000-0000000000c1-zagolovok',
    )
  })

  it('without the author still leads to the post by its blog id', () => {
    const wrapper = mountCard(item(paragraphs(1), { author: null }, null))
    expect(wrapper.find('[data-testid="feed-card-author"]').exists()).toBe(false)
    expect(wrapper.find('h2').exists()).toBe(false)
    expect(wrapper.get('[data-testid="feed-card-date"]').attributes('href')).toBe(
      `/u/${anna.id}/0195f3a0-0000-7000-8000-0000000000c1`,
    )
  })

  it('shows a short post whole, with no «Показать полностью»', () => {
    const wrapper = mountCard(item(paragraphs(3)))
    expect(wrapper.findAll('.post-blocks__text')).toHaveLength(3)
    expect(wrapper.find('[data-testid="feed-card-more"]').exists()).toBe(false)
  })

  it('cuts a longread to its first blocks and opens the rest in place', async () => {
    const wrapper = mountCard(item(paragraphs(6)))
    expect(wrapper.findAll('.post-blocks__text')).toHaveLength(3)
    expect(wrapper.text()).not.toContain('Абзац 4.')
    const more = wrapper.get('[data-testid="feed-card-more"]')
    expect(more.text()).toBe('Показать полностью')

    await more.trigger('click')

    expect(wrapper.findAll('.post-blocks__text')).toHaveLength(6)
    expect(wrapper.text()).toContain('Абзац 6.')
    expect(wrapper.find('[data-testid="feed-card-more"]').exists()).toBe(false)
  })

  // E08-15, ADR-0062: the best comment under the card, chosen by the server; none for a post without comments.
  it('has no best comment block for a post without comments', () => {
    const wrapper = mountCard(item(paragraphs(1)))
    expect(wrapper.find('[data-testid="feed-best-comment"]').exists()).toBe(false)
  })

  it('shows the best comment with its author and leads to it on the post page', () => {
    const boris: FeedBlog = {
      ...anna,
      id: '0195f3a0-0000-7000-8000-0000000000b2',
      name: 'Борис',
      nickname: 'boris',
    }
    const id = '0195f3a0-0000-7000-8000-0000000000d1'
    const wrapper = mountCard(
      item(paragraphs(6), { bestComment: { id, author: boris, guestName: null, text: 'Хороший пост.' } }),
    )
    const block = wrapper.get('[data-testid="feed-best-comment"]')
    expect(block.get('[data-testid="feed-best-comment-author"]').text()).toBe('Борис')
    expect(block.get('[data-testid="feed-best-comment-text"]').text()).toBe('Хороший пост.')
    expect(block.attributes('href')).toBe(
      `/u/anna/0195f3a0-0000-7000-8000-0000000000c1-zagolovok#comment-${id}`,
    )
    // The last block of the card: under the post, even a longread cut short.
    expect(wrapper.get('article').element.lastElementChild).toBe(block.element)
  })

  it('names the outside author of a guest comment', () => {
    const wrapper = mountCard(
      item(paragraphs(1), {
        bestComment: {
          id: '0195f3a0-0000-7000-8000-0000000000d2',
          author: null,
          guestName: 'Гость из Telegram',
          text: 'Привет из канала.',
        },
      }),
    )
    expect(wrapper.get('[data-testid="feed-best-comment-author"]').text()).toBe('Гость из Telegram')
  })
})
