import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import TopPeriodSwitch from '../../components/feed/TopPeriodSwitch.vue'

// The switch of the period in «Популярном» (E08-06, ADR-0104); the pages against the fake API are in e2e/top.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

describe('TopPeriodSwitch', () => {
  it('leads to the address of every period, in order', () => {
    const wrapper = mount(TopPeriodSwitch, { props: { current: 'week' }, global: { stubs } })
    const links = wrapper.findAll('a')
    expect(links.map((link) => link.text())).toEqual([
      'За день',
      'За неделю',
      'За месяц',
      'За год',
      'За всё время',
    ])
    expect(links.map((link) => link.attributes('href'))).toEqual([
      '/top/day',
      '/top/week',
      '/top/month',
      '/top/year',
      '/top/all',
    ])
    expect(wrapper.get('nav').attributes('aria-label')).toBe('Период')
  })

  it('marks only the shown period as the current page', () => {
    const wrapper = mount(TopPeriodSwitch, { props: { current: 'all' }, global: { stubs } })
    const current = wrapper.findAll('[aria-current="page"]')
    expect(current).toHaveLength(1)
    expect(current[0]!.text()).toBe('За всё время')
  })
})
