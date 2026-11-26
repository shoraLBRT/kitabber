// E14-02 (#85, ADR-0097): the query history in the browser — the latest first, no repeats, at most ten, one list per
// account and one for a guest — and the key the site's server derives from the account.
import { beforeEach, describe, expect, it } from 'vitest'
import {
  clearSearchHistory,
  forgetSearch,
  readSearchHistory,
  rememberSearch,
  searchHistoryKey,
  searchHistoryLimit,
} from '../../utils/search-history'
import { searchHistoryKeyOf } from '../../server/utils/search-history-key'

const key = searchHistoryKey('k1')

beforeEach(() => localStorage.clear())

describe('search history', () => {
  it('keeps the latest query first, without repeats in another case or with other spaces', () => {
    rememberSearch(localStorage, key, 'коты')
    rememberSearch(localStorage, key, 'собаки')
    const history = rememberSearch(localStorage, key, '  Коты ')

    expect(history).toEqual(['Коты', 'собаки'])
    expect(readSearchHistory(localStorage, key)).toEqual(['Коты', 'собаки'])
  })

  it('keeps at most ten queries and ignores a blank one', () => {
    for (let i = 0; i < searchHistoryLimit + 3; i++) rememberSearch(localStorage, key, `запрос ${i}`)
    rememberSearch(localStorage, key, '   ')

    const history = readSearchHistory(localStorage, key)
    expect(history).toHaveLength(searchHistoryLimit)
    expect(history[0]).toBe(`запрос ${searchHistoryLimit + 2}`)
  })

  it('forgets one query or all of them', () => {
    rememberSearch(localStorage, key, 'a')
    rememberSearch(localStorage, key, 'b')

    expect(forgetSearch(localStorage, key, 'a')).toEqual(['b'])
    expect(clearSearchHistory(localStorage, key)).toEqual([])
    expect(localStorage.getItem(key)).toBeNull()
  })

  it('a guest and each account have lists of their own', () => {
    rememberSearch(localStorage, searchHistoryKey('k1'), 'первый')
    rememberSearch(localStorage, searchHistoryKey(null), 'гость')

    expect(searchHistoryKey(undefined)).toBe('search.history.guest')
    expect(readSearchHistory(localStorage, searchHistoryKey('k2'))).toEqual([])
    expect(readSearchHistory(localStorage, searchHistoryKey(null))).toEqual(['гость'])
  })

  it('reads a broken or foreign value as no history, and works without storage', () => {
    localStorage.setItem(key, '{not json')
    expect(readSearchHistory(localStorage, key)).toEqual([])
    localStorage.setItem(key, JSON.stringify([1, 'ok', '']))
    expect(readSearchHistory(localStorage, key)).toEqual(['ok'])
    expect(rememberSearch(undefined, key, 'q')).toEqual(['q'])
  })
})

describe('search history key', () => {
  const anna = '0195f3a0-0000-7000-8000-000000000001'

  it('is stable for an account, different for another, and does not show the account id', () => {
    const first = searchHistoryKeyOf(anna, 'secret')

    expect(searchHistoryKeyOf(anna.toUpperCase(), 'secret')).toBe(first)
    expect(searchHistoryKeyOf('0195f3a0-0000-7000-8000-000000000002', 'secret')).not.toBe(first)
    expect(searchHistoryKeyOf(anna, 'another secret')).not.toBe(first)
    expect(first).toMatch(/^[\w-]{22}$/)
    expect(first).not.toContain('0195f3a0')
  })
})
