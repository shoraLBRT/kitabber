// The widget «Популярные комментарии» of the desktop's right column (E08-12, ADR-0105): asked in the browser after
// the page shows (server/routes/bff/popular-comments.get.ts), so a page render never waits on it, and only while the
// right column is on the screen (`shown`): a phone and a narrow window never ask. Asked again when the person signs
// in or out, since the authors they blocked are left out. The authors' names and avatars follow, as in the comments.
import { computed, shallowRef, watch, type Ref } from 'vue'
import { loadAuthors } from '../api/comments'
import type { PopularComments } from '../api/popular-comments'
import type { Profile } from '../api/profiles'
import type { WidgetState } from '../components/shell/ShellWidget.vue'

export const popularCommentsKey = 'popular-comments'

/** The state of the widget from the read's status and what it gave. */
export function popularCommentsState(
  status: 'idle' | 'pending' | 'success' | 'error',
  found: PopularComments | null,
): WidgetState {
  if (status === 'error') return 'error'
  if (status !== 'success' || !found) return 'loading'
  return found.comments.length > 0 ? 'ready' : 'empty'
}

export function usePopularComments(shown: Ref<boolean>) {
  const { $api } = useNuxtApp()
  const account = useAccount()
  const authors = shallowRef(new Map<string, Profile | null>())
  const { data, status, refresh } = useAsyncData(
    popularCommentsKey,
    async () => {
      if (!shown.value) return null
      const found = await $fetch<PopularComments>('/bff/popular-comments')
      const ids = [...new Set(found.comments.map((comment) => comment.authorAccountId))].filter(
        (id) => !authors.value.has(id),
      )
      // A new map, not the old one changed: the list sees a new prop and draws the names.
      if (ids.length > 0) authors.value = new Map([...authors.value, ...(await loadAuthors($api, ids))])
      return found
    },
    { server: false, lazy: true, default: () => null, watch: [shown] },
  )
  watch(
    () => account.value.signedIn,
    () => refresh(),
  )
  const state = computed(() => popularCommentsState(status.value, data.value))
  const comments = computed(() => data.value?.comments ?? [])
  return { state, comments, authors, refresh }
}
