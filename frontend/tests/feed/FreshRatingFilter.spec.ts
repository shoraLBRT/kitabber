import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import FreshRatingFilter from '../../components/feed/FreshRatingFilter.vue'
import { freshRatingValue, isFreshRating } from '../../api/feeds'
import {
  accountFreshRating,
  freshRatingStorageKey,
  readFreshRating,
  rememberFreshRating,
} from '../../utils/fresh-rating'

// The rating filter of «Свежее» (E08-07, ADR-0106); the page against the fake API is in e2e/fresh-rating.spec.ts and the
// filter itself is applied by the server (FreshFeedRatingApiTests).
describe('FreshRatingFilter', () => {
  it('offers every post and the thresholds of the spec, in order', () => {
    const wrapper = mount(FreshRatingFilter, { props: { current: null } })
    const options = wrapper.findAll('button')
    expect(options.map((option) => option.text())).toEqual(['Все', 'От −10', 'От +5', 'От +10'])
    expect(wrapper.get('[role="group"]').attributes('aria-label')).toBe('Рейтинг')
  })

  it('presses only the threshold in use', () => {
    const wrapper = mount(FreshRatingFilter, { props: { current: 5 } })
    const pressed = wrapper.findAll('[aria-pressed="true"]')
    expect(pressed).toHaveLength(1)
    expect(pressed[0]!.text()).toBe('От +5')
  })

  it('asks for another threshold, and not for the one in use', async () => {
    const wrapper = mount(FreshRatingFilter, { props: { current: 5 } })
    await wrapper.get('[data-rating="10"]').trigger('click')
    await wrapper.get('[data-rating="5"]').trigger('click')
    await wrapper.get('[data-rating="all"]').trigger('click')
    expect(wrapper.emitted('choose')).toEqual([[10], [null]])
  })
})

describe('the thresholds', () => {
  it('know their values and how they read', () => {
    expect([null, -10, 5, 10].every(isFreshRating)).toBe(true)
    expect([0, 7, '5', undefined].some(isFreshRating)).toBe(false)
    expect(freshRatingValue(-10)).toBe('−10')
    expect(freshRatingValue(10)).toBe('+10')
  })

  it('come from the account, anything unknown reading as «все»', () => {
    expect(accountFreshRating({ feeds: { freshMinRating: 10 } })).toBe(10)
    expect(accountFreshRating({ feeds: { freshMinRating: 7 } })).toBeNull()
    expect(accountFreshRating({ feeds: { freshMinRating: null } })).toBeNull()
    expect(accountFreshRating(null)).toBeNull()
  })
})

describe('the guest’s threshold in this browser', () => {
  function memoryStorage() {
    const items = new Map<string, string>()
    return {
      items,
      getItem: (key: string) => items.get(key) ?? null,
      setItem: (key: string, value: string) => void items.set(key, value),
      removeItem: (key: string) => void items.delete(key),
    }
  }

  it('is remembered, and «все» leaves no entry', () => {
    const storage = memoryStorage()
    expect(readFreshRating(storage)).toBeNull()
    rememberFreshRating(storage, -10)
    expect(storage.items.get(freshRatingStorageKey)).toBe('-10')
    expect(readFreshRating(storage)).toBe(-10)
    rememberFreshRating(storage, null)
    expect(storage.items.has(freshRatingStorageKey)).toBe(false)
  })

  it('reads as «все» when the entry is not a threshold or storage is blocked', () => {
    const storage = memoryStorage()
    storage.items.set(freshRatingStorageKey, '7')
    expect(readFreshRating(storage)).toBeNull()
    const blocked = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {
        throw new Error('blocked')
      },
      removeItem: () => {
        throw new Error('blocked')
      },
    }
    expect(readFreshRating(blocked)).toBeNull()
    expect(() => rememberFreshRating(blocked, 5)).not.toThrow()
  })
})
