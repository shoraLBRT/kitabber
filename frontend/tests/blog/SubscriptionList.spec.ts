import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SubscriptionList, { type SubscriptionListState } from '../../components/blog/SubscriptionList.vue'
import BlogSubscription from '../../components/blog/BlogSubscription.vue'
import { appendSubscriptionCards, type SubscriptionCard } from '../../api/subscriptions'

// The subscriber and subscription lists (E04-06, ADR-0099): rows, states and «Подписаться»; the dialog against the
// fake API is in e2e/subscription-lists.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const anna: SubscriptionCard = {
  id: '0195f3a0-0000-7000-8000-0000000000c1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  avatarSources: [],
  subscribed: true,
  own: false,
}
const me: SubscriptionCard = {
  ...anna,
  id: '0195f3a0-0000-7000-8000-0000000000c2',
  name: 'Я',
  nickname: null,
  own: true,
  subscribed: false,
}
const games: SubscriptionCard = {
  ...anna,
  id: '0195f3a0-0000-7000-8000-0000000000c3',
  type: 'topic',
  name: 'Игры',
  nickname: 'games',
  subscribed: false,
}

function mountList(state: SubscriptionListState, props: Record<string, unknown> = {}) {
  return mount(SubscriptionList, { props: { kind: 'subscribers', state, ...props }, global: { stubs } })
}

describe('SubscriptionList', () => {
  it('lists blogs and topics leading to their pages, with the reader’s marks and no button on their own blog', () => {
    const w = mountList({ status: 'found', cards: [anna, me, games], hasMore: false })
    const rows = w.findAll('[data-testid="subscription-card"]')
    expect(rows.map((row) => row.get('a').attributes('href'))).toEqual(['/u/anna', `/u/${me.id}`, '/games'])
    expect(rows.map((row) => row.get('[data-testid="subscription-card-name"]').text())).toEqual([
      'Анна',
      'Я',
      'Игры',
    ])
    expect(rows[2]!.text()).toContain('Тема')
    expect(rows[1]!.find('[data-testid="subscription-card-button"]').exists()).toBe(false)
    const anna_ = rows[0]!.get('[data-testid="subscription-card-button"]')
    expect(anna_.text()).toBe('Вы подписаны')
    expect(anna_.attributes('aria-pressed')).toBe('true')
    expect(rows[2]!.get('[data-testid="subscription-card-button"]').text()).toBe('Подписаться')
  })

  it('reports a press, holds a busy row and asks for the next page', async () => {
    const w = mountList({ status: 'found', cards: [anna, games], hasMore: true }, { busy: [games.id] })
    const buttons = w.findAll('[data-testid="subscription-card-button"]')
    await buttons[0]!.trigger('click')
    expect(w.emitted('toggle')).toEqual([[anna]])
    expect(buttons[1]!.attributes('disabled')).toBeDefined()
    await w.get('[data-testid="subscription-list-more"]').trigger('click')
    expect(w.emitted('more')).toHaveLength(1)
  })

  it('shows the loading, empty and error states', async () => {
    expect(mountList({ status: 'loading' }).find('[data-testid="subscription-list-loading"]').exists()).toBe(
      true,
    )
    expect(mountList({ status: 'found', cards: [], hasMore: false }).text()).toContain(
      'Пока никто не подписан.',
    )
    expect(
      mountList({ status: 'found', cards: [], hasMore: false }, { kind: 'subscriptions' }).text(),
    ).toContain('Пока ни на кого не подписан.')
    const failed = mountList({ status: 'error' })
    expect(failed.text()).toContain('Не удалось загрузить список.')
    await failed.get('[data-testid="subscription-list-retry"]').trigger('click')
    expect(failed.emitted('retry')).toHaveLength(1)
  })

  it('says when the next page failed and offers to repeat it', () => {
    const w = mountList({ status: 'found', cards: [anna], hasMore: true }, { moreFailed: true })
    expect(w.get('[role="alert"]').text()).toContain('Не удалось загрузить ещё')
    expect(w.get('[data-testid="subscription-list-more"]').text()).toBe('Повторить')
  })
})

describe('appendSubscriptionCards', () => {
  it('adds a later page without repeating a row already shown', () => {
    expect(
      appendSubscriptionCards([anna], { items: [anna, games], nextCursor: null }).map((card) => card.id),
    ).toEqual([anna.id, games.id])
  })
})

describe('BlogSubscription with a blog', () => {
  it('makes the numbers buttons that open the lists', () => {
    const w = mount(BlogSubscription, {
      props: {
        state: { subscriberCount: 3, subscribed: false, notifyNewPosts: false },
        subscriptionCount: 2,
        blogId: anna.id,
      },
      global: { stubs },
    })
    expect(w.get('[data-testid="subscriber-count"]').element.tagName).toBe('BUTTON')
    expect(w.get('[data-testid="subscription-count"]').attributes('aria-haspopup')).toBe('dialog')
  })

  it('keeps plain numbers without a blog', () => {
    const w = mount(BlogSubscription, {
      props: { state: { subscriberCount: 3, subscribed: false, notifyNewPosts: false } },
    })
    expect(w.get('[data-testid="subscriber-count"]').element.tagName).toBe('SPAN')
  })
})
