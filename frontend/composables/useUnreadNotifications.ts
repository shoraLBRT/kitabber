// The number of the person's unread notifications (E12-02, ADR-0093): the counter on the header's bell and on the
// phone's «Уведомления» tab. Asked in the browser after the page shows (server/routes/bff/notifications/unread.get.ts),
// so a page render never waits on it; asked again when the person signs in or out. Marking rows read answers with the
// new number, which the list puts here with `set`. A guest and a failed read show no counter.
//
// In real time (E12-04, ADR-0123) the account's topic brings `notifications.changed` — a new notification, or rows
// marked read in another tab — and every open tab asks for the number again; so does a tab that reconnects after a
// break, whose events are lost. The number the server gives last is the one every tab shows.
import { computed, onBeforeUnmount, onMounted, watch } from 'vue'
import { notificationsApi, unreadOf } from '../api/notifications'
import type { RealtimeChannel } from '../realtime/channel'

export const unreadNotificationsKey = 'notifications:unread'

/** The realtime event of the own account's topic: its notifications changed (ADR-0123). It carries no data. */
export const notificationsChangedType = 'notifications.changed'

/** Calls `changed` on every `notifications.changed` of the own account and after every reconnect; returns the stop. */
export function followNotificationChanges(
  channel: Pick<RealtimeChannel, 'on' | 'onReconnect'>,
  changed: () => void,
): () => void {
  const offEvent = channel.on((event) => {
    if (event.type === notificationsChangedType && event.topic.startsWith('account:')) changed()
  })
  const offReconnect = channel.onReconnect(changed)
  return () => {
    offEvent()
    offReconnect()
  }
}

export function useUnreadNotifications() {
  const account = useAccount()
  const { data, refresh } = useAsyncData(
    unreadNotificationsKey,
    async () => (account.value.signedIn ? (unreadOf(await notificationsApi().unread()) ?? 0) : 0),
    { server: false, lazy: true, default: () => 0 },
  )
  watch(
    () => account.value.signedIn,
    () => refresh(),
  )
  const unread = computed(() => (account.value.signedIn ? (data.value ?? 0) : 0))
  function set(count: number) {
    data.value = count
  }
  return { unread, set, refresh }
}

/**
 * Keeps the counter live while the component is mounted (the layout, once per page): `changed` is called on each
 * change of the account's notifications. On the server and for a guest there is no channel and nothing happens.
 */
export function useNotificationChanges(changed: () => void): void {
  const channel = useRealtime()
  let off: (() => void) | null = null
  onMounted(() => {
    if (channel) off = followNotificationChanges(channel, changed)
  })
  onBeforeUnmount(() => off?.())
}
