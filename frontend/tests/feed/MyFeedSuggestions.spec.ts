import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import MyFeedSuggestions from '../../components/feed/MyFeedSuggestions.vue'

// What an empty «Моя лента» offers (E08-04, ADR-0089); the page with the feed against the fake API is in
// e2e/my-feed.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const topic = (n: number) => ({ id: `t${n}`, name: `Тема ${n}`, nickname: `topic${n}` })

describe('MyFeedSuggestions', () => {
  it('offers at most six topics, each leading to its page, and the fresh feed', () => {
    const wrapper = mount(MyFeedSuggestions, {
      props: { topics: Array.from({ length: 8 }, (_, n) => topic(n + 1)) },
      global: { stubs },
    })
    const links = wrapper.findAll('[data-testid="my-feed-topic"]')
    expect(links).toHaveLength(6)
    expect(links[0]!.text()).toBe('Тема 1')
    expect(links[0]!.attributes('href')).toBe('/topic1')
    expect(wrapper.get('[data-testid="my-feed-fresh"]').attributes('href')).toBe('/new')
  })

  it('offers only the fresh feed when there are no topics', () => {
    const wrapper = mount(MyFeedSuggestions, { props: { topics: [] }, global: { stubs } })
    expect(wrapper.find('ul').exists()).toBe(false)
    expect(wrapper.get('[data-testid="my-feed-fresh"]').text()).toBe('Найти авторов в «Свежем»')
  })
})
