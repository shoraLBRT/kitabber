// E14-02 (#85, ADR-0097): the header's search — the history of an empty field and «Очистить», suggestions after a pause
// in typing with the answers of older queries dropped, the loading, empty, failed and too-long states, and the
// keyboard: ↑ ↓ pick a row, Enter opens it, Esc closes the search.
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ShellSearch from '../../components/shell/ShellSearch.vue'
import {
  normalizeQuery,
  suggestDelay,
  suggestedBlogName,
  suggestedBlogPath,
  suggestedPostPath,
  type SearchApi,
  type SearchOutcome,
  type SearchSuggestions,
} from '../../api/search'
import { searchHistoryKey } from '../../utils/search-history'
import { global } from './fixtures'

const found: SearchSuggestions = {
  query: 'кош',
  blogs: [
    { id: 'b1', kind: 'personal', name: 'Анна о кошках', nickname: 'anna', avatarSources: [] },
    { id: 't1', kind: 'topic', name: 'Кошки', nickname: 'cats', avatarSources: [] },
  ],
  posts: [
    {
      id: 'p1',
      blogId: 'b1',
      title: 'Как воспитать кошку',
      excerpt: 'Терпение и корм.',
      publishedAt: '2026-11-26T10:00:00Z',
    },
  ],
}
const nothing: SearchSuggestions = { query: 'щщ', blogs: [], posts: [] }

function fakeApi(
  answer: (query: string) => Promise<SearchOutcome> | SearchOutcome = () => ({ status: 200, body: found }),
) {
  return { suggest: vi.fn(async (query: string) => answer(query)) } satisfies SearchApi
}

let wrapper: VueWrapper | undefined

function mountSearch(api: SearchApi = fakeApi(), historyKey: string | null = 'k1') {
  wrapper = mount(ShellSearch, { props: { api, historyKey }, global, attachTo: document.body })
  return wrapper
}

async function type(view: VueWrapper, text: string) {
  await view.get('[data-testid="search-input"]').setValue(text)
  await vi.advanceTimersByTimeAsync(suggestDelay)
  await flushPromises()
}

function key(view: VueWrapper, name: string) {
  return view.get('[data-testid="search-input"]').trigger('keydown', { key: name })
}

beforeEach(() => {
  vi.useFakeTimers()
  localStorage.clear()
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.useRealTimers()
})

describe('ShellSearch', () => {
  it('asks for suggestions only after a pause in typing, with the query trimmed', async () => {
    const api = fakeApi()
    const view = mountSearch(api)

    await view.get('[data-testid="search-input"]').setValue('к')
    await vi.advanceTimersByTimeAsync(suggestDelay - 100)
    await view.get('[data-testid="search-input"]').setValue('  кош ')
    await vi.advanceTimersByTimeAsync(suggestDelay - 100)
    expect(api.suggest).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(100)
    await flushPromises()

    expect(api.suggest).toHaveBeenCalledTimes(1)
    expect(api.suggest).toHaveBeenCalledWith('кош', expect.any(AbortSignal))
    const blogs = view.findAll('[data-testid="search-blog"]')
    expect(blogs.map((b) => b.text())).toEqual(['АОАнна о кошкахБлог', 'ККошкиТема'])
    expect(blogs[0]!.get('a').attributes('href')).toBe('/u/anna')
    expect(blogs[1]!.get('a').attributes('href')).toBe('/cats')
    const post = view.get('[data-testid="search-post"]')
    expect(post.text()).toContain('Как воспитать кошку')
    expect(post.get('a').attributes('href')).toBe('/u/b1/p1')
  })

  it('shows the skeleton while asking, and drops the answer of an older query', async () => {
    const pending: Record<string, (outcome: SearchOutcome) => void> = {}
    const api = fakeApi((q) => new Promise((resolve) => (pending[q] = resolve)))
    const view = mountSearch(api)

    await type(view, 'кош')
    expect(view.find('[data-testid="search-loading"]').exists()).toBe(true)
    await type(view, 'кошка')
    pending['кошка']!({ status: 200, body: nothing })
    await flushPromises()
    pending['кош']!({ status: 200, body: found })
    await flushPromises()

    expect(view.find('[data-testid="search-blog"]').exists()).toBe(false)
    expect(view.get('[data-testid="search-empty"]').text()).toBe('Ничего не нашлось по запросу «кошка».')
  })

  it('a failed answer offers «Повторить», which asks again', async () => {
    let fail = true
    const api = fakeApi(() => (fail ? { status: 502 } : { status: 200, body: found }))
    const view = mountSearch(api)

    await type(view, 'кош')
    expect(view.get('[data-testid="search-error"]').text()).toContain('Не удалось получить подсказки.')
    fail = false
    await view.get('[data-testid="search-error"] button').trigger('click')
    await flushPromises()

    expect(api.suggest).toHaveBeenCalledTimes(2)
    expect(view.findAll('[data-testid="search-blog"]')).toHaveLength(2)
  })

  it('a query longer than the API takes is not sent', async () => {
    const api = fakeApi()
    const view = mountSearch(api)

    await type(view, 'а'.repeat(101))

    expect(api.suggest).not.toHaveBeenCalled()
    expect(view.find('[data-testid="search-too-long"]').exists()).toBe(true)
  })

  it('an empty field shows the history, which «Очистить» empties', async () => {
    localStorage.setItem(searchHistoryKey('k1'), JSON.stringify(['коты', 'астрономия']))
    const view = mountSearch()
    await flushPromises()

    const rows = view.findAll('[data-testid="search-history-row"]')
    expect(rows.map((r) => r.text())).toEqual(['коты', 'астрономия'])
    await view.get('[data-testid="search-history-clear"]').trigger('click')

    expect(view.find('[data-testid="search-history-row"]').exists()).toBe(false)
    expect(view.find('[data-testid="search-history-empty"]').exists()).toBe(true)
    expect(localStorage.getItem(searchHistoryKey('k1'))).toBeNull()
  })

  it('one query leaves the history by its cross, a picked one fills the field', async () => {
    localStorage.setItem(searchHistoryKey('k1'), JSON.stringify(['коты', 'астрономия']))
    const api = fakeApi()
    const view = mountSearch(api)
    await flushPromises()

    await view.get('[aria-label="Убрать «коты» из истории"]').trigger('click')
    expect(view.findAll('[data-testid="search-history-row"]').map((r) => r.text())).toEqual(['астрономия'])
    await view.get('[data-testid="search-history-row"] button').trigger('click')
    await vi.advanceTimersByTimeAsync(suggestDelay)
    await flushPromises()

    expect((view.get('[data-testid="search-input"]').element as HTMLInputElement).value).toBe('астрономия')
    expect(api.suggest).toHaveBeenCalledWith('астрономия', expect.any(AbortSignal))
  })

  it('one account never sees the history of another or of a guest', async () => {
    localStorage.setItem(searchHistoryKey('k1'), JSON.stringify(['запрос первого']))
    localStorage.setItem(searchHistoryKey(null), JSON.stringify(['запрос гостя']))

    const other = mountSearch(fakeApi(), 'k2')
    await flushPromises()
    expect(other.find('[data-testid="search-history-row"]').exists()).toBe(false)
    other.unmount()

    const guest = mountSearch(fakeApi(), null)
    await flushPromises()
    expect(guest.findAll('[data-testid="search-history-row"]').map((r) => r.text())).toEqual(['запрос гостя'])
  })

  it('↑ and ↓ pick a row, Enter opens it and keeps the query in the history', async () => {
    const view = mountSearch()
    await type(view, 'кош')
    const input = view.get('[data-testid="search-input"]')
    const links = view.findAll('[role="option"] a')
    const clicked: string[] = []
    for (const link of links)
      (link.element as HTMLAnchorElement).addEventListener('click', (event) => {
        event.preventDefault()
        clicked.push((link.element as HTMLAnchorElement).getAttribute('href')!)
      })

    await key(view, 'ArrowDown')
    expect(input.attributes('aria-activedescendant')).toBe('search-option-0')
    await key(view, 'ArrowDown')
    await key(view, 'ArrowDown')
    expect(view.get('#search-option-2').attributes('aria-selected')).toBe('true')
    await key(view, 'ArrowDown')
    expect(input.attributes('aria-activedescendant')).toBe('search-option-0')
    await key(view, 'ArrowUp')
    expect(input.attributes('aria-activedescendant')).toBe('search-option-2')
    await key(view, 'Enter')

    expect(clicked).toEqual(['/u/b1/p1'])
    expect(view.emitted('close')).toHaveLength(1)
    expect(JSON.parse(localStorage.getItem(searchHistoryKey('k1'))!)).toEqual(['кош'])
  })

  it('Enter with no row picked keeps the query and asks for its results; Esc and the backdrop close the search', async () => {
    const view = mountSearch()
    await type(view, '  кошки   и  собаки ')

    await key(view, 'Enter')
    expect(JSON.parse(localStorage.getItem(searchHistoryKey('k1'))!)).toEqual(['кошки и собаки'])
    // The results page (E14-03): the layout opens it and closes the search.
    expect(view.emitted('search')).toEqual([['кошки и собаки']])
    expect(view.emitted('close')).toBeUndefined()
    await key(view, 'Escape')
    await view.get('[data-testid="search-backdrop"]').trigger('click')

    expect(view.emitted('close')).toHaveLength(2)
  })

  it('the field takes the focus when the search opens', () => {
    const view = mountSearch()

    expect(document.activeElement).toBe(view.get('[data-testid="search-input"]').element)
  })
})

describe('search helpers', () => {
  it('trims a query and collapses its spaces', () => {
    expect(normalizeQuery('  кот \t учёный  ')).toBe('кот учёный')
    expect(normalizeQuery('   ')).toBe('')
  })

  it('names a blog by its name, else its nickname', () => {
    expect(suggestedBlogName({ name: ' Анна ', nickname: 'anna' })).toBe('Анна')
    expect(suggestedBlogName({ name: null, nickname: 'anna' })).toBe('@anna')
  })

  it('leads to a blog or a topic by its nickname, else by its id, and to a post by its blog id', () => {
    expect(suggestedBlogPath({ id: 'b1', kind: 'personal', nickname: null })).toBe('/u/b1')
    expect(suggestedBlogPath({ id: 't1', kind: 'topic', nickname: 'cats' })).toBe('/cats')
    expect(suggestedPostPath({ id: 'p1', blogId: 'b1' })).toBe('/u/b1/p1')
  })
})
