import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SearchResults, { type FoundBlock } from '../../components/search/SearchResults.vue'
import SearchPostFilters from '../../components/search/SearchPostFilters.vue'
import {
  appendFound,
  defaultSearchFilters,
  filtered,
  filtersOf,
  resultsQuery,
  searchResultsPath,
  type FoundBlog,
  type FoundPost,
  type SearchFilters,
} from '../../api/search'

// The search results (E14-03, ADR-0124): the blocks, their states, «Подписаться» in the rows, the filters of «Посты»,
// and the helpers that keep the query and the filters in the address. The page against the fake API is in
// e2e/search-results.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const anna: FoundBlog = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  avatarSources: [],
  subscribed: false,
  own: false,
}
const books: FoundBlog = {
  ...anna,
  id: '0195f3a0-0000-7000-8000-0000000000b4',
  type: 'topic',
  name: 'Книги',
  nickname: 'books',
}
const post: FoundPost = {
  id: '0195f3a0-0000-7000-8000-00000000a101',
  blogId: anna.id,
  title: null,
  excerpt: 'Дочитал книги из отпуска.',
  publishedAt: '2026-11-20T10:00:00Z',
  blog: { id: anna.id, kind: 'personal', name: 'Анна', nickname: 'anna', avatarSources: [] },
}

function found<T>(items: T[], hasMore = false): FoundBlock<T> {
  return { status: 'found', items, hasMore, loadingMore: false, moreFailed: false }
}

function state<T>(status: 'loading' | 'error'): FoundBlock<T> {
  return { status, items: [], hasMore: false, loadingMore: false, moreFailed: false }
}

function mountResults(props: Partial<InstanceType<typeof SearchResults>['$props']> = {}) {
  return mount(SearchResults, {
    props: {
      query: 'книг',
      blogs: found([anna]),
      topics: found([books], true),
      posts: found([post]),
      filters: defaultSearchFilters,
      ...props,
    },
    global: { stubs },
  })
}

describe('SearchResults', () => {
  it('shows the blocks «Блоги», «Темы» and «Посты» with «Подписаться» in the blog rows', () => {
    const view = mountResults()

    expect(view.get('[data-testid="search-block-blogs"] h2').text()).toBe('Блоги')
    expect(view.get('[data-testid="search-block-topics"] h2').text()).toBe('Темы')
    expect(view.get('[data-testid="search-block-posts"] h2').text()).toBe('Посты')
    const buttons = view.findAll('[data-testid="subscription-card-button"]')
    expect(buttons.map((b) => b.text())).toEqual(['Подписаться', 'Подписаться'])
    const row = view.get('[data-testid="search-result-post"]')
    expect(row.get('a').attributes('href')).toBe(`/u/${anna.id}/${post.id}`)
    expect(row.text()).toContain('Пост без заголовка')
    expect(row.text()).toContain('Дочитал книги из отпуска.')
    expect(row.text()).toContain('Анна')
  })

  it('reports a press on «Подписаться» and «Показать ещё» of a block', async () => {
    const view = mountResults()

    await view
      .get('[data-testid="search-block-topics"] [data-testid="subscription-card-button"]')
      .trigger('click')
    await view
      .get('[data-testid="search-block-topics"] [data-testid="subscription-list-more"]')
      .trigger('click')

    expect(view.emitted('toggle')).toEqual([[books]])
    expect(view.emitted('more')).toEqual([['topics']])
  })

  it('leaves out a block that found nothing and the own blog has no button', () => {
    const view = mountResults({ blogs: found([{ ...anna, own: true }]), topics: found([]) })

    expect(view.find('[data-testid="search-block-topics"]').exists()).toBe(false)
    expect(view.find('[data-testid="subscription-card-button"]').exists()).toBe(false)
  })

  it('shows a placeholder when nothing at all is found', () => {
    const view = mountResults({ blogs: found([]), topics: found([]), posts: found([]) })

    expect(view.get('[data-testid="search-results-empty"]').text()).toBe(
      'По запросу «книг» ничего не нашлось.',
    )
    expect(view.find('[data-testid="search-block-posts"]').exists()).toBe(false)
  })

  it('keeps «Посты» with its filters open while a filter finds nothing', () => {
    const filters: SearchFilters = { ...defaultSearchFilters, titlesOnly: true }
    const view = mountResults({ blogs: found([]), topics: found([]), posts: found([]), filters })

    expect(view.find('[data-testid="search-results-empty"]').exists()).toBe(false)
    expect(view.get('[data-testid="search-posts-empty"]').text()).toBe(
      'Постов с такими фильтрами не нашлось.',
    )
    expect(view.get('[data-testid="search-filters-toggle"]').attributes('aria-expanded')).toBe('true')
  })

  it('shows skeletons while searching and an error with «Повторить» when every block failed', async () => {
    const loading = mountResults({
      blogs: state('loading'),
      topics: state('loading'),
      posts: state('loading'),
    })
    expect(loading.find('[data-testid="search-results-loading"]').exists()).toBe(true)

    const failed = mountResults({ blogs: state('error'), topics: state('error'), posts: state('error') })
    expect(failed.text()).toContain('Не удалось выполнить поиск.')
    await failed.get('[data-testid="search-results-retry"]').trigger('click')
    expect(failed.emitted('retry')).toHaveLength(1)
  })

  it('opens the filters with «Фильтры» and reports a change', async () => {
    const view = mountResults()
    const toggle = view.get('[data-testid="search-filters-toggle"]')
    expect(toggle.attributes('aria-expanded')).toBe('false')

    await toggle.trigger('click')
    expect(toggle.attributes('aria-expanded')).toBe('true')
    await view.getComponent(SearchPostFilters).vm.$emit('change', { ...defaultSearchFilters, order: 'date' })

    expect(view.emitted('filters')).toEqual([[{ ...defaultSearchFilters, order: 'date' }]])
  })
})

describe('SearchPostFilters', () => {
  it('reports «Только заголовки», a period and an order, the chosen one pressed', async () => {
    const view = mount(SearchPostFilters, { props: { filters: { ...defaultSearchFilters, period: 'week' } } })

    expect(view.get('[data-period="week"]').attributes('aria-pressed')).toBe('true')
    await view.get('[data-period="week"]').trigger('click')
    await view.get('[data-period="year"]').trigger('click')
    await view.get('[data-order="date"]').trigger('click')
    await view.get('[data-testid="search-filter-titles"]').setValue(true)

    expect(view.emitted('change')).toEqual([
      [{ titlesOnly: false, period: 'year', order: 'relevance' }],
      [{ titlesOnly: false, period: 'week', order: 'date' }],
      [{ titlesOnly: true, period: 'week', order: 'relevance' }],
    ])
  })
})

describe('search results helpers', () => {
  it('keeps the query and the filters in the address, the defaults left out', () => {
    expect(searchResultsPath('  кошки   и собаки ')).toBe(
      '/rating?q=%D0%BA%D0%BE%D1%88%D0%BA%D0%B8%20%D0%B8%20%D1%81%D0%BE%D0%B1%D0%B0%D0%BA%D0%B8',
    )
    expect(resultsQuery('кот', defaultSearchFilters)).toEqual({ q: 'кот' })
    expect(resultsQuery('кот', { titlesOnly: true, period: 'month', order: 'date' })).toEqual({
      q: 'кот',
      titles: '1',
      period: 'month',
      order: 'date',
    })
  })

  it('reads the filters from the address, an unknown value as the default', () => {
    expect(filtersOf({ titles: '1', period: 'day', order: 'date' })).toEqual({
      titlesOnly: true,
      period: 'day',
      order: 'date',
    })
    expect(filtersOf({ titles: 'yes', period: 'century', order: ['date'] })).toEqual(defaultSearchFilters)
    expect(filtered(defaultSearchFilters)).toBe(false)
    expect(filtered({ ...defaultSearchFilters, period: 'year' })).toBe(true)
  })

  it('appends a page without the rows already shown', () => {
    expect(appendFound([anna], [anna, books]).map((b) => b.id)).toEqual([anna.id, books.id])
  })
})
