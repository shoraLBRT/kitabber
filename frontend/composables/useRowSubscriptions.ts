// «Подписаться» in the rows of a list of blogs and topics (E04-06, ADR-0099; the search results of E14-03, ADR-0124):
// a press changes the row at once through /bff/subscription and goes back with a toast when the server refuses; a
// guest's press opens the sign-in window (ADR-0032). The list keeps its rows; `mark` sets a row's mark in them.
import { ref } from 'vue'
import type { SubscriptionChange } from '../api/subscriptions'
import { t } from '../i18n'

interface SubscribableRow {
  id: string
  subscribed: boolean
}

/** `action` names the pending action of a guest's press, so it is made once they are signed in. */
export function useRowSubscriptions(
  mark: (id: string, subscribed: boolean) => void,
  action: (id: string) => string = (id) => `subscribe-list:${id}`,
) {
  const requireAccount = useRequireAccount()
  const toast = useToast()
  /** The rows whose subscription is being changed: their buttons wait. */
  const busy = ref<string[]>([])

  async function change(row: SubscribableRow) {
    if (busy.value.includes(row.id)) return
    const wanted = !row.subscribed
    busy.value = [...busy.value, row.id]
    mark(row.id, wanted)
    const result = await $fetch<SubscriptionChange>('/bff/subscription', {
      method: 'POST',
      body: { blogId: row.id, subscribed: wanted },
    }).catch((): SubscriptionChange => ({ status: 'error' }))
    busy.value = busy.value.filter((id) => id !== row.id)
    if (result.status === 'done') {
      mark(row.id, result.state.subscribed)
      return
    }
    mark(row.id, row.subscribed)
    toast.show(
      result.status === 'refused'
        ? result.message
        : result.status === 'unauthorized'
          ? t('subscription.signedOut')
          : t('subscription.failed'),
      { tone: 'error' },
    )
  }

  function toggle(row: SubscribableRow) {
    requireAccount(action(row.id), () => void change(row))
  }

  return { busy, toggle }
}
