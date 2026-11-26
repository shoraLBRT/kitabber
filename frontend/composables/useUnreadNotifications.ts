// The number of the person's unread notifications (E12-02, ADR-0093): the counter on the header's bell and on the
// phone's «Уведомления» tab. Asked in the browser after the page shows (server/routes/bff/notifications/unread.get.ts),
// so a page render never waits on it; asked again when the person signs in or out. Marking rows read answers with the
// new number, which the list puts here with `set`. A guest and a failed read show no counter.
import { computed, watch } from 'vue'
import { notificationsApi, unreadOf } from '../api/notifications'

export const unreadNotificationsKey = 'notifications:unread'

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
