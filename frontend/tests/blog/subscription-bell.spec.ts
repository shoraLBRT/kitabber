import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import { actionText } from '../../api/notifications'
import {
  changeNewPostNotifications,
  subscriptionOf,
  toggledBell,
  toggledSubscription,
} from '../../api/subscriptions'
import BlogSubscription from '../../components/blog/BlogSubscription.vue'

// The bell of a subscription (E11-04, FR-NOTIFY, ADR-0118) without a page: the API's answers, the state shown before
// the server answers, the button next to «Вы подписаны» and the row of the notification. The pages against the fake
// API are in e2e/subscription-bell.spec.ts.

const blogId = '0195f3a0-0000-7000-8000-0000000000b5'

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

describe('changeNewPostNotifications', () => {
  it('turns the bell on with PUT and off with DELETE, answering the new state', async () => {
    const state = { subscriberCount: 3, subscribed: true, notifyNewPosts: true }
    const { client, fetch } = clientAnswering({ status: 200, body: state })
    expect(await changeNewPostNotifications(client, blogId, true)).toEqual({ status: 'done', state })
    await changeNewPostNotifications(client, blogId, false)
    const requests = fetch.mock.calls.map(([request]) => request)
    expect(requests.map((r) => r.method)).toEqual(['PUT', 'DELETE'])
    expect(new URL(requests[0]!.url).pathname).toBe(`/api/v1/blogs/${blogId}/subscription/notifications`)
  })

  it('turns 401 into signed out, 409 and 404 into the reason, anything else into a failure', async () => {
    const answer = (status: number, body: unknown = {}) =>
      changeNewPostNotifications(clientAnswering({ status, body }).client, blogId, true)
    expect(await answer(401)).toEqual({ status: 'unauthorized' })
    expect(await answer(409, { detail: 'Сначала подпишитесь.' })).toEqual({
      status: 'refused',
      message: 'Сначала подпишитесь.',
    })
    expect(await answer(409)).toEqual({
      status: 'refused',
      message: 'Уведомления о постах включаются только у подписки.',
    })
    expect((await answer(404)).status).toBe('refused')
    expect(await answer(500)).toEqual({ status: 'error' })
  })
})

describe('the bell state', () => {
  it('turns the other way at once and is dropped with the subscription', () => {
    const on = { subscriberCount: 2, subscribed: true, notifyNewPosts: true }
    expect(toggledBell(on)).toEqual({ ...on, notifyNewPosts: false })
    expect(toggledBell({ ...on, notifyNewPosts: false })).toEqual(on)
    expect(toggledSubscription(on)).toEqual({ subscriberCount: 1, subscribed: false, notifyNewPosts: false })
  })

  it('takes the bell of a profile, off when the profile has none', () => {
    expect(
      subscriptionOf({ subscriberCount: 1, subscribed: true, notifyNewPosts: true }).notifyNewPosts,
    ).toBe(true)
    expect(subscriptionOf({ subscriberCount: 1, subscribed: true }).notifyNewPosts).toBe(false)
  })

  it('names a new post in a subscription in the notification row', () => {
    expect(actionText({ type: 'subscription_post' })).toBe('опубликовал новый пост')
  })
})

describe('BlogSubscription with the bell', () => {
  it('shows a subscriber the bell, unpressed, and reports a press', async () => {
    const wrapper = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1, subscribed: true, notifyNewPosts: false } },
    })
    const bell = wrapper.get('[data-testid="subscription-bell"]')
    expect(bell.attributes('aria-pressed')).toBe('false')
    expect(bell.attributes('aria-label')).toBe('Уведомлять о новых постах')
    await bell.trigger('click')
    expect(wrapper.emitted('bell')).toHaveLength(1)
    expect(wrapper.emitted('toggle')).toBeUndefined()
  })

  it('shows the bell pressed when it is on', () => {
    const wrapper = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1, subscribed: true, notifyNewPosts: true } },
    })
    const bell = wrapper.get('[data-testid="subscription-bell"]')
    expect(bell.attributes('aria-pressed')).toBe('true')
    expect(bell.attributes('aria-label')).toBe('Не уведомлять о новых постах')
  })

  it('has no bell without a subscription, for the owner, and keeps it disabled while busy', () => {
    const guest = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1, subscribed: false, notifyNewPosts: false } },
    })
    expect(guest.find('[data-testid="subscription-bell"]').exists()).toBe(false)
    const owner = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1, subscribed: true, notifyNewPosts: false }, own: true },
    })
    expect(owner.find('[data-testid="subscription-bell"]').exists()).toBe(false)
    const busy = mount(BlogSubscription, {
      props: { state: { subscriberCount: 1, subscribed: true, notifyNewPosts: false }, busy: true },
    })
    expect(busy.get('[data-testid="subscription-bell"]').attributes('disabled')).toBeDefined()
  })
})
