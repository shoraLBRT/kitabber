// The subscription of a blog or topic page (E11-03, FR-SUB, docs/ui/blog.md, ADR-0085): the numbers and the reader's
// own mark arrive with the profile; a press changes the button and the count at once, the server's answer then
// replaces them, and a failure puts the old ones back with a toast. A guest's press opens the sign-in window
// (ADR-0032); back on the page signed in, the subscription is made by itself.
import { onMounted, ref, watch } from 'vue'
import {
  subscribeAction,
  subscriptionOf,
  toggledSubscription,
  type SubscriptionChange,
  type SubscriptionView,
} from '../api/subscriptions'
import type { Profile } from '../api/profiles'
import { tabStorage, takePendingAction } from '../utils/pending-action'
import { t } from '../i18n'

// The person's own access token stays on the site's server (server/routes/bff/subscription.post.ts).
async function send(blogId: string, subscribed: boolean): Promise<SubscriptionChange> {
  try {
    return await $fetch<SubscriptionChange>('/bff/subscription', {
      method: 'POST',
      body: { blogId, subscribed },
    })
  } catch {
    return { status: 'error' }
  }
}

/** The subscription of the shown blog `profile`; `null` while there is none (loading, a failure). */
export function useSubscription(profile: () => Profile | null) {
  const account = useAccount()
  const requireAccount = useRequireAccount()
  const route = useRoute()
  const toast = useToast()
  const state = ref<SubscriptionView>({ subscriberCount: 0, subscribed: false })
  const busy = ref(false)

  watch(
    () => {
      const shown = profile()
      return shown ? [shown.id, shown.subscriberCount, shown.subscribed] : null
    },
    () => {
      const shown = profile()
      if (shown && !busy.value) state.value = subscriptionOf(shown)
    },
    { immediate: true },
  )

  async function set(subscribed: boolean) {
    const blogId = profile()?.id
    const before = state.value
    if (!blogId || busy.value || before.subscribed === subscribed) return
    busy.value = true
    state.value = toggledSubscription(before)
    const result = await send(blogId, subscribed)
    busy.value = false
    if (result.status === 'done') {
      state.value = result.state
      return
    }
    state.value = before
    toast.show(
      result.status === 'refused'
        ? result.message
        : result.status === 'unauthorized'
          ? t('subscription.signedOut')
          : t('subscription.failed'),
      { tone: 'error' },
    )
  }

  /** A press on «Подписаться» or «Вы подписаны»: a guest gets the sign-in window instead. */
  function toggle() {
    const blogId = profile()?.id
    if (!blogId) return
    requireAccount(subscribeAction(blogId), () => void set(!state.value.subscribed))
  }

  // The subscription a guest asked for before the sign-in: made once the profile is shown signed in.
  onMounted(() => {
    watch(
      () => profile()?.id ?? null,
      (blogId) => {
        if (
          blogId &&
          account.value.signedIn &&
          takePendingAction(tabStorage(), subscribeAction(blogId), route.fullPath)
        )
          void set(true)
      },
      { immediate: true },
    )
  })

  return { state, busy, toggle }
}
