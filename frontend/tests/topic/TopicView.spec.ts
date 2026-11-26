import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import TopicView from '../../components/topic/TopicView.vue'
import type { Profile } from '../../api/profiles'

// The topic page's header and states (E11-01, docs/ui/topic.md, ADR-0082); the server render, the addresses and the
// posts against the fake API are in e2e/topic.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const books: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b4',
  type: 'topic',
  name: 'Книги',
  nickname: 'books',
  description: 'Что читать и как читать.',
  registeredAt: null,
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
  subscriberCount: 0,
  subscribed: false,
  cover: null,
  owner: { id: '0195f3a0-0000-7000-8000-0000000000b2', name: 'Борис', nickname: 'boris' },
}

type State = InstanceType<typeof TopicView>['$props']['state']

function mountView(state: State) {
  return mount(TopicView, {
    props: { state },
    slots: { default: '<p data-testid="topic-posts">посты</p>' },
    global: { stubs },
  })
}

describe('TopicView', () => {
  it('shows the header of a topic: name, description, the owner with a link to their blog, then the posts', () => {
    const wrapper = mountView({ status: 'found', profile: books })
    expect(wrapper.get('header h1').text()).toBe('Книги')
    expect(wrapper.get('[data-testid="topic-description"]').text()).toBe('Что читать и как читать.')
    const owner = wrapper.get('[data-testid="topic-owner"]')
    expect(owner.text()).toBe('Владелец темы: Борис')
    expect(owner.get('a').attributes('href')).toBe('/u/boris')
    expect(wrapper.get('[data-testid="topic-posts"]').text()).toBe('посты')
  })

  it('leaves out the description and the owner a topic does not have', () => {
    const wrapper = mountView({ status: 'found', profile: { ...books, description: null, owner: null } })
    expect(wrapper.find('[data-testid="topic-description"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="topic-owner"]').exists()).toBe(false)
  })

  // E11-03, ADR-0085: a topic shows its subscribers and «Подписаться»; it has no «подписки» of its own.
  it('shows the subscribers of a topic and «Подписаться», whose press goes to the page', async () => {
    const wrapper = mountView({ status: 'found', profile: { ...books, subscriberCount: 21 } })
    expect(wrapper.get('[data-testid="subscription-counts"]').text()).toBe('21 подписчик')
    await wrapper.get('[data-testid="subscribe-button"]').trigger('click')
    expect(wrapper.emitted('subscribe')).toHaveLength(1)
  })

  it('shows a skeleton of the header while it loads, and no posts', () => {
    const wrapper = mountView({ status: 'loading' })
    expect(wrapper.get('[data-testid="topic-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.text()).toContain('Загрузка темы')
    expect(wrapper.find('[data-testid="topic-posts"]').exists()).toBe(false)
  })

  it('shows a failure with «Повторить»', async () => {
    const wrapper = mountView({ status: 'error' })
    expect(wrapper.get('[data-testid="topic-error"]').text()).toContain('Не удалось загрузить тему')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})
