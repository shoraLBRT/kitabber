// E12-02 (#83, ADR-0093): the notification feed — loading, empty and failed states, the rows with the person who acted
// and where they lead, the next page by the cursor, and marking the shown unread rows read. E12-03 (#115, ADR-0122):
// a grouped row («Борис и ещё 9 оценили вашу публикацию»), one row per group across pages, and «Прочитать все».
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import NotificationList from '../../components/notifications/NotificationList.vue'
import {
  actionText,
  actorName,
  appendNotifications,
  badgeText,
  notificationLink,
  othersText,
  unreadOf,
  type Notification,
  type NotificationsApi,
  type NotificationsOutcome,
} from '../../api/notifications'
import { useToast } from '../../composables/useToast'
import { NuxtLink } from '../shell/fixtures'

const toast = useToast()

const boris = {
  accountId: '0195f3a0-0000-7000-8000-000000000002',
  blogId: '0195f3a0-0000-7000-8000-0000000000b2',
  name: 'Борис',
  nickname: 'boris',
  avatarSources: [],
}

function notification(id: string, overrides: Partial<Notification> = {}): Notification {
  return {
    id,
    type: 'comment_reply',
    actor: boris,
    postId: '0195f3a0-0000-7000-8000-00000000a001',
    commentId: '0195f3a0-0000-7000-8000-00000000c001',
    reactionKind: null,
    createdAt: '2026-11-26T10:00:00Z',
    read: false,
    group: id,
    actorCount: 1,
    ...overrides,
  }
}

const first = notification('n1')
const second = notification('n2', { type: 'new_subscriber', postId: null, commentId: null, read: true })
const third = notification('n3', { type: 'reaction', commentId: null, reactionKind: 'like' })

function fakeApi(overrides: Partial<NotificationsApi> = {}): NotificationsApi {
  return {
    load: vi.fn(async (cursor?: string | null) =>
      cursor === 'c1'
        ? { status: 200, body: { items: [third], nextCursor: null } }
        : { status: 200, body: { items: [first, second], nextCursor: 'c1' } },
    ),
    unread: vi.fn(async () => ({ status: 200, body: { unread: 2 } })),
    markRead: vi.fn(async (ids: readonly string[]) => ({ status: 200, body: { unread: 2 - ids.length } })),
    markAllRead: vi.fn(async () => ({ status: 200, body: { unread: 0 } })),
    ...overrides,
  }
}

let wrapper: VueWrapper | undefined

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  toast.clear()
})

async function mounted(api: NotificationsApi, compact = false) {
  wrapper = mount(NotificationList, {
    props: { api, compact },
    attachTo: document.body,
    global: { components: { NuxtLink } },
  })
  await flushPromises()
  return wrapper
}

const rows = (w: VueWrapper) => w.findAll('[data-testid="notification"]')

describe('NotificationList', () => {
  it('shows five skeleton rows until the feed is read, then the rows newest first', async () => {
    let finish: (outcome: NotificationsOutcome) => void = () => {}
    const api = fakeApi({
      load: vi.fn(() => new Promise<NotificationsOutcome>((resolve) => (finish = resolve))),
    })
    const w = await mounted(api)
    expect(w.find('[data-testid="notifications-loading"]').exists()).toBe(true)
    expect(w.findAll('.notifications__skeleton')).toHaveLength(5)
    expect(w.get('[role="status"]').text()).toBe('Загрузка уведомлений')

    finish({ status: 200, body: { items: [first, second], nextCursor: null } })
    await flushPromises()
    expect(rows(w).map((r) => r.text())).toEqual([
      expect.stringContaining('Борис ответил на ваш комментарий'),
      expect.stringContaining('Борис подписался на ваш блог'),
    ])
    expect(rows(w)[0]!.get('a').attributes('href')).toBe(`/u/-/${first.postId}#comment-${first.commentId}`)
    expect(rows(w)[1]!.get('a').attributes('href')).toBe('/u/boris')
    expect(rows(w)[0]!.classes()).toContain('notifications__item--unread')
    expect(rows(w)[1]!.classes()).not.toContain('notifications__item--unread')
    expect(w.find('[data-testid="notifications-more"]').exists()).toBe(false)
  })

  it('marks the unread rows it showed read and gives the new number up', async () => {
    const api = fakeApi()
    const w = await mounted(api)
    expect(api.markRead).toHaveBeenCalledWith(['n1'])
    expect(w.emitted('unread')).toEqual([[1]])
    // The dot stays until the list is opened again.
    expect(rows(w)[0]!.attributes('data-read')).toBe('false')
  })

  it('reads the next page by the cursor of the page before and marks its rows too', async () => {
    const api = fakeApi()
    const w = await mounted(api)
    await w.get('[data-testid="notifications-more"]').trigger('click')
    await flushPromises()
    expect(api.load).toHaveBeenLastCalledWith('c1')
    expect(rows(w)).toHaveLength(3)
    expect(rows(w)[2]!.text()).toContain('Борис оценил вашу публикацию')
    expect(api.markRead).toHaveBeenLastCalledWith(['n3'])
    expect(w.find('[data-testid="notifications-more"]').exists()).toBe(false)
  })

  it('shows ten likes of one post as one row', async () => {
    const likes = notification('n4', {
      type: 'reaction',
      commentId: null,
      reactionKind: 'like',
      actorCount: 10,
    })
    const load = vi.fn(async () => ({ status: 200, body: { items: [likes, first], nextCursor: null } }))
    const w = await mounted(fakeApi({ load }))
    expect(rows(w)).toHaveLength(2)
    expect(rows(w)[0]!.text()).toContain('Борис и ещё 9 оценили вашу публикацию')
    expect(rows(w)[1]!.text()).toContain('Борис ответил на ваш комментарий')
  })

  it('«Прочитать все» takes every dot off and gives the counter 0', async () => {
    const api = fakeApi()
    const w = await mounted(api)
    expect(rows(w)[0]!.attributes('data-read')).toBe('false')
    await w.get('[data-testid="notifications-read-all"]').trigger('click')
    await flushPromises()
    expect(api.markAllRead).toHaveBeenCalledTimes(1)
    expect(rows(w).map((r) => r.attributes('data-read'))).toEqual(['true', 'true'])
    expect(w.emitted('unread')?.at(-1)).toEqual([0])
  })

  it('brings the dots back with a toast when «Прочитать все» fails', async () => {
    const api = fakeApi({ markAllRead: vi.fn(async () => ({ status: 502 })) })
    const w = await mounted(api)
    await w.get('[data-testid="notifications-read-all"]').trigger('click')
    await flushPromises()
    expect(rows(w).map((r) => r.attributes('data-read'))).toEqual(['false', 'true'])
    expect(toast.toasts.at(-1)?.message).toBe(
      'Не удалось отметить уведомления прочитанными. Попробуйте ещё раз.',
    )
    expect(w.emitted('unread')).toEqual([[1]])
  })

  it('offers no «Прочитать все» when every row is read and there is no next page', async () => {
    const load = vi.fn(async () => ({ status: 200, body: { items: [second], nextCursor: null } }))
    const w = await mounted(fakeApi({ load }))
    expect(w.find('[data-testid="notifications-read-all"]').exists()).toBe(false)
  })

  it('keeps the rows when the next page fails and offers «Повторить»', async () => {
    const load = vi.fn(async (cursor?: string | null) =>
      cursor ? { status: 502 } : { status: 200, body: { items: [first], nextCursor: 'c1' } },
    )
    const w = await mounted(fakeApi({ load }))
    await w.get('[data-testid="notifications-more"]').trigger('click')
    await flushPromises()
    expect(rows(w)).toHaveLength(1)
    expect(w.get('[role="alert"]').text()).toBe('Не удалось загрузить следующие уведомления.')
    expect(w.get('[data-testid="notifications-more"]').text()).toBe('Повторить')
  })

  it('says so when there is nothing yet, shorter in the panel', async () => {
    const api = fakeApi({ load: vi.fn(async () => ({ status: 200, body: { items: [], nextCursor: null } })) })
    const page = await mounted(api)
    expect(page.get('[data-testid="notifications-empty"]').text()).toContain(
      'Уведомлений пока нет. Сюда придут',
    )
    page.unmount()
    const panel = await mounted(api, true)
    expect(panel.get('[data-testid="notifications-empty"]').text()).toBe('Уведомлений пока нет')
    expect(api.markRead).not.toHaveBeenCalled()
  })

  it('offers «Повторить» when the feed could not be read, and reads it again', async () => {
    const load = vi
      .fn<NotificationsApi['load']>()
      .mockResolvedValueOnce({ status: 0 })
      .mockResolvedValue({ status: 200, body: { items: [second], nextCursor: null } })
    const w = await mounted(fakeApi({ load }))
    expect(w.get('[role="alert"]').text()).toBe('Не удалось загрузить уведомления')
    await w.get('[data-testid="notifications-error"] button').trigger('click')
    await flushPromises()
    expect(rows(w)).toHaveLength(1)
  })
})

describe('notification helpers', () => {
  it('names the person by the blog name, the nickname or «Пользователь»', () => {
    expect(actorName(first)).toBe('Борис')
    expect(actorName({ actor: { ...boris, name: null } })).toBe('@boris')
    expect(actorName({ actor: { ...boris, name: null, nickname: null } })).toBe('Пользователь')
  })

  it('tells every type and a plain row for an unknown one', () => {
    expect(actionText({ type: 'post_comment' })).toBe('прокомментировал ваш пост')
    expect(actionText({ type: 'mention' })).toBe('упомянул вас в комментарии')
    expect(actionText({ type: 'achievement' })).toBe('что-то сделал')
  })

  it('tells a grouped row in the plural with the number of the others', () => {
    expect(othersText({ actorCount: 1 })).toBeNull()
    expect(othersText({})).toBeNull()
    expect(othersText({ actorCount: 10 })).toBe('и ещё 9')
    expect(actionText({ type: 'reaction', actorCount: 10 })).toBe('оценили вашу публикацию')
    expect(actionText({ type: 'new_subscriber', actorCount: 2 })).toBe('подписались на ваш блог')
    expect(actionText({ type: 'post_comment', actorCount: 3 })).toBe('прокомментировали ваш пост')
    expect(actionText({ type: 'repost', actorCount: 4 })).toBe('сделали репост вашего поста')
    expect(actionText({ type: 'mention', actorCount: 2 })).toBe('упомянул вас в комментарии')
  })

  it('keeps one row per group across pages', () => {
    const lifted = notification('n7', { group: 'reaction:p1:', actorCount: 11 })
    const shown = [notification('n6', { group: 'reaction:p1:', actorCount: 10 }), first]
    expect(appendNotifications(shown, [lifted, third]).map((n) => n.id)).toEqual(['n6', 'n1', 'n3'])
  })

  it('leads a subscriber without a nickname to the blog by its id, and nowhere without a blog', () => {
    expect(notificationLink({ ...second, actor: { ...boris, nickname: null } })).toBe(`/u/${boris.blogId}`)
    expect(notificationLink({ ...second, actor: { ...boris, nickname: null, blogId: null } })).toBeNull()
    expect(notificationLink(third)).toBe(`/u/-/${third.postId}`)
  })

  // E11-12, ADR-0121: an offer of a topic leads to the topic's management page, where it is accepted or declined.
  it("leads an offer of a topic to the topic's management page", () => {
    const offer = notification('n9', {
      type: 'topic_transfer_offer',
      postId: null,
      commentId: null,
      blogId: '0195f3a0-0000-7000-8000-0000000000b4',
    })
    expect(actionText(offer)).toBe('предлагает вам тему')
    expect(notificationLink(offer)).toBe('/topics/0195f3a0-0000-7000-8000-0000000000b4/manage')
    expect(notificationLink({ ...offer, blogId: null })).toBe('/u/boris')
  })

  it('reads the unread number and writes the counter', () => {
    expect(unreadOf({ status: 200, body: { unread: 4 } })).toBe(4)
    expect(unreadOf({ status: 401 })).toBeNull()
    expect(badgeText(99)).toBe('99')
    expect(badgeText(100)).toBe('99+')
  })
})
