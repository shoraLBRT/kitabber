// The counter in real time (E12-04, ADR-0123): `notifications.changed` of the own account's topic and a reconnect
// after a break each ask for the unread number again; other events and other topics do not, and stopping ends both.
import { describe, expect, it, vi } from 'vitest'
import { followNotificationChanges, notificationsChangedType } from '../../composables/useUnreadNotifications'
import type { RealtimeEvent, RealtimeHandler } from '../../realtime/channel'

function fakeChannel() {
  const handlers = new Set<RealtimeHandler>()
  const reconnects = new Set<() => void>()
  return {
    on(handler: RealtimeHandler) {
      handlers.add(handler)
      return () => handlers.delete(handler)
    },
    onReconnect(handler: () => void) {
      reconnects.add(handler)
      return () => reconnects.delete(handler)
    },
    push(event: RealtimeEvent) {
      for (const handler of handlers) handler(event)
    },
    reconnect() {
      for (const handler of reconnects) handler()
    },
    get listeners() {
      return handlers.size + reconnects.size
    },
  }
}

describe('notification changes', () => {
  it('is the event the backend sends', () => {
    expect(notificationsChangedType).toBe('notifications.changed')
  })

  it('asks again on a change of the own account and after a reconnect', () => {
    const channel = fakeChannel()
    const changed = vi.fn()
    const stop = followNotificationChanges(channel, changed)

    channel.push({ topic: 'account:a', type: 'notifications.changed', data: null })
    channel.push({ topic: 'post:1', type: 'notifications.changed', data: null })
    channel.push({ topic: 'account:a', type: 'comments.posted', data: null })
    expect(changed).toHaveBeenCalledTimes(1)

    channel.reconnect()
    expect(changed).toHaveBeenCalledTimes(2)

    stop()
    expect(channel.listeners).toBe(0)
    channel.push({ topic: 'account:a', type: 'notifications.changed', data: null })
    channel.reconnect()
    expect(changed).toHaveBeenCalledTimes(2)
  })
})
