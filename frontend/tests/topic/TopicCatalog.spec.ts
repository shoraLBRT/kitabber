import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import TopicCatalog, { type CatalogState } from '../../components/topic/TopicCatalog.vue'
import type { TopicCard } from '../../api/topics'

// The topic catalogue's look and states (E11-02, ADR-0088); the page against the fake API is in e2e/topics.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const books: TopicCard = {
  id: '0195f3a0-0000-7000-8000-0000000000b4',
  name: 'Книги',
  nickname: 'books',
  description: 'Что читать и как читать.',
  subscriberCount: 41,
}
const science: TopicCard = {
  id: '0195f3a0-0000-7000-8000-0000000000b5',
  name: 'Наука',
  nickname: 'science',
  description: null,
  subscriberCount: 0,
}

function mountCatalog(state: CatalogState, props: Record<string, unknown> = {}) {
  return mount(TopicCatalog, { props: { state, ...props }, global: { stubs } })
}

describe('TopicCatalog', () => {
  it('lists the topics in the given order, each leading to its page with its description and subscribers', () => {
    const w = mountCatalog({ status: 'found', topics: [books, science], hasMore: false })
    const cards = w.findAll('[data-testid="topic-card"]')
    expect(cards.map((card) => card.attributes('href'))).toEqual(['/books', '/science'])
    expect(cards[0]!.text()).toContain('Что читать и как читать.')
    expect(cards.map((card) => card.get('[data-testid="topic-subscribers"]').text())).toEqual([
      '41 подписчик',
      '0 подписчиков',
    ])
    expect(w.find('[data-testid="topic-catalog-more"]').exists()).toBe(false)
  })

  it('offers the next page and asks for it', async () => {
    const w = mountCatalog({ status: 'found', topics: [books], hasMore: true })
    await w.get('[data-testid="topic-catalog-more"]').trigger('click')
    expect(w.emitted('more')).toHaveLength(1)
  })

  it('says when the next page failed and offers it again', () => {
    const w = mountCatalog({ status: 'found', topics: [books], hasMore: true }, { moreFailed: true })
    expect(w.get('[role="alert"]').text()).toBe('Не удалось загрузить ещё темы.')
    expect(w.get('[data-testid="topic-catalog-more"]').text()).toBe('Повторить')
  })

  it('shows skeletons while loading, told to screen readers', () => {
    const w = mountCatalog({ status: 'loading' })
    const loading = w.get('[data-testid="topic-catalog-loading"]')
    expect(loading.attributes('aria-busy')).toBe('true')
    expect(loading.get('[role="status"]').text()).toBe('Загрузка тем')
    expect(w.find('[data-testid="topic-card"]').exists()).toBe(false)
  })

  it('shows an error with «Повторить»', async () => {
    const w = mountCatalog({ status: 'error' })
    expect(w.get('[data-tone="error"]').text()).toContain('Не удалось загрузить темы.')
    await w.get('[data-tone="error"] button').trigger('click')
    expect(w.emitted('retry')).toHaveLength(1)
  })

  it('tells an empty catalogue from a search without results', () => {
    expect(
      mountCatalog({ status: 'found', topics: [], hasMore: false }).get('[data-tone="empty"]').text(),
    ).toBe('Тем пока нет.')
    expect(
      mountCatalog({ status: 'found', topics: [], hasMore: false }, { searched: ' кино ' })
        .get('[data-tone="empty"]')
        .text(),
    ).toBe('По запросу «кино» тем не нашлось.')
  })

  it('has a labelled search field no longer than the API takes, bound to `search`', async () => {
    const w = mountCatalog({ status: 'found', topics: [], hasMore: false }, { search: 'на' })
    const input = w.get('input[type="search"]')
    expect(input.attributes('aria-label')).toBe('Поиск по темам')
    expect(input.attributes('maxlength')).toBe('50')
    expect((input.element as HTMLInputElement).value).toBe('на')
    await input.setValue('наука')
    expect(w.emitted('update:search')?.at(-1)).toEqual(['наука'])
  })
})
