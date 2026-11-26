import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import {
  changeSubscription,
  compactCount,
  subscribeAction,
  subscriptionOf,
  toggledSubscription,
} from '../../api/subscriptions'
import BlogSubscription from '../../components/blog/BlogSubscription.vue'

// Subscribing to a blog or a topic (E11-03, FR-SUB, ADR-0085) without a page: the API's answers, the state shown
// before the server answers, the numbers and the button. The pages against the fake API are in e2e/subscriptions.spec.ts.

const blogId = '0195f3a0-0000-7000-8000-0000000000b4'

function clientAnswering(answer: { status: number; body: unknown }) {
  const fetch = vi.fn(
    async (_request: Request) =>
      new Response(JSON.stringify(answer.body), {
        status: answer.status,
        headers: { 'content-type': 'application/json' },
      }),
  )
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

describe('changeSubscription', () => {
  it('subscribes with PUT and unsubscribes with DELETE, answering the new numbers', async () => {
    const { client, fetch } = clientAnswering({ status: 200, body: { subscriberCount: 3, subscribed: true } })
    expect(await changeSubscription(client, blogId, true)).toEqual({
      status: 'done',
      state: { subscriberCount: 3, subscribed: true },
    })
    await changeSubscription(client, blogId, false)
    const requests = fetch.mock.calls.map(([request]) => request)
    expect(requests.map((r) => r.method)).toEqual(['PUT', 'DELETE'])
    expect(new URL(requests[0]!.url).pathname).toBe(`/api/v1/blogs/${blogId}/subscription`)
  })

  it('turns 401 into signed out, 403 and 404 into the reason, anything else into a failure', async () => {
    expect(await changeSubscription(clientAnswering({ status: 401, body: {} }).client, blogId, true)).toEqual(
      {
        status: 'unauthorized',
      },
    )
    expect(
      await changeSubscription(
        clientAnswering({ status: 403, body: { detail: 'На свой блог подписаться нельзя.' } }).client,
        blogId,
        true,
      ),
    ).toEqual({ status: 'refused', message: 'На свой блог подписаться нельзя.' })
    expect(
      await changeSubscription(clientAnswering({ status: 404, body: {} }).client, blogId, false),
    ).toEqual({
      status: 'refused',
      message: 'Блог или тема не найдены: возможно, их удалили.',
    })
    expect(await changeSubscription(clientAnswering({ status: 500, body: {} }).client, blogId, true)).toEqual(
      {
        status: 'error',
      },
    )
  })
})

describe('the subscription state', () => {
  it('moves the count by one at once and never below zero', () => {
    expect(toggledSubscription({ subscriberCount: 4, subscribed: false })).toEqual({
      subscriberCount: 5,
      subscribed: true,
    })
    expect(toggledSubscription({ subscriberCount: 0, subscribed: true })).toEqual({
      subscriberCount: 0,
      subscribed: false,
    })
  })

  it('takes the numbers of a profile and names the action a guest repeats after the sign-in', () => {
    expect(subscriptionOf({ subscriberCount: 2, subscribed: true })).toEqual({
      subscriberCount: 2,
      subscribed: true,
    })
    expect(subscribeAction(blogId)).toBe(`subscribe:${blogId}`)
  })

  it('shortens counts from a thousand on', () => {
    expect(compactCount(999)).toBe('999')
    expect(compactCount(1200)).toMatch(/^1,2\s?тыс\.$/)
  })
})

describe('BlogSubscription', () => {
  it('shows the subscribers and the owner’s subscriptions with «Подписаться», and reports a press', async () => {
    const wrapper = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1200, subscribed: false }, subscriptionCount: 34 },
    })
    expect(wrapper.get('[data-testid="subscriber-count"]').text()).toMatch(/^1,2\s?тыс\. подписчиков$/)
    expect(wrapper.get('[data-testid="subscription-count"]').text()).toBe('34 подписки')
    const button = wrapper.get('[data-testid="subscribe-button"]')
    expect(button.text()).toBe('Подписаться')
    expect(button.attributes('aria-pressed')).toBe('false')
    await button.trigger('click')
    expect(wrapper.emitted('toggle')).toHaveLength(1)
  })

  it('shows «Вы подписаны» to a subscriber and a topic without the subscriptions count', () => {
    const wrapper = mount(BlogSubscription, { props: { state: { subscriberCount: 1, subscribed: true } } })
    expect(wrapper.get('[data-testid="subscriber-count"]').text()).toBe('1 подписчик')
    expect(wrapper.find('[data-testid="subscription-count"]').exists()).toBe(false)
    const button = wrapper.get('[data-testid="subscribe-button"]')
    expect(button.text()).toBe('Вы подписаны')
    expect(button.attributes('aria-pressed')).toBe('true')
  })

  it('shows the owner the numbers without the button, and an empty blog zero subscribers', () => {
    const wrapper = mount(BlogSubscription, {
      props: { state: { subscriberCount: 0, subscribed: false }, subscriptionCount: 0, own: true },
    })
    expect(wrapper.get('[data-testid="subscriber-count"]').text()).toBe('0 подписчиков')
    expect(wrapper.get('[data-testid="subscription-count"]').text()).toBe('0 подписок')
    expect(wrapper.find('[data-testid="subscribe-button"]').exists()).toBe(false)
  })

  it('keeps the button disabled while the change is not answered', () => {
    const wrapper = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1, subscribed: true }, busy: true },
    })
    expect(wrapper.get('[data-testid="subscribe-button"]').attributes('disabled')).toBeDefined()
  })
})
