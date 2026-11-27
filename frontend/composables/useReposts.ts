// The reposts of a post on its page (E10-08, ADR-0117): the list is read in the browser through
// server/routes/bff/reposts.get.ts, so it carries the reader's token and leaves out the people they blocked; reposting
// goes through server/routes/bff/repost.post.ts. After a repost the list is read again, so the reader sees their own
// repost at its top. «Показать ещё» reads the next page with the cursor of the one before. The first page is read only
// when the section asks for it (`start`), once it comes near the screen.
import { computed, ref } from 'vue'
import { appendReposts, type Repost, type RepostOutcome, type RepostPage } from '../api/reposts'
import { t } from '../i18n'

/** What the list shows: a skeleton, the error with «Повторить», «Репостов пока нет», or the reposts. */
export type RepostsState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'empty' }
  | { status: 'found'; reposts: Repost[]; hasMore: boolean }

export function useReposts(postId: () => string) {
  const shown = ref<Repost[] | null>(null)
  const cursor = ref<string | null>(null)
  const loadingMore = ref(false)
  const moreFailed = ref(false)
  const sending = ref(false)
  const sendError = ref<string | null>(null)
  const reposted = ref(false)

  const read = (after?: string | null) =>
    $fetch<RepostPage>('/bff/reposts', { query: { postId: postId(), ...(after ? { cursor: after } : {}) } })

  const status = ref<'pending' | 'success' | 'error'>('pending')

  async function refresh() {
    status.value = 'pending'
    try {
      const page = await read()
      shown.value = page.items
      cursor.value = page.nextCursor ?? null
      moreFailed.value = false
      status.value = 'success'
    } catch {
      status.value = 'error'
    }
  }

  // Read in the browser only, when the list comes near the screen: it is under the comments, so a long tree is drawn
  // and used first without the list competing with it.
  let started = false
  /** Reads the first page once; the section calls it when it comes near the screen. */
  function start() {
    if (started) return
    started = true
    void refresh()
  }

  const state = computed<RepostsState>(() => {
    if (status.value === 'error') return { status: 'error' }
    if (status.value !== 'success' || !shown.value) return { status: 'loading' }
    if (shown.value.length === 0) return { status: 'empty' }
    return { status: 'found', reposts: shown.value, hasMore: cursor.value !== null }
  })

  async function loadMore() {
    if (!cursor.value || !shown.value || loadingMore.value) return
    loadingMore.value = true
    moreFailed.value = false
    try {
      const page = await read(cursor.value)
      shown.value = appendReposts(shown.value, page.items)
      cursor.value = page.nextCursor ?? null
    } catch {
      moreFailed.value = true
    } finally {
      loadingMore.value = false
    }
  }

  /** Reposts the post with `text`; `true` once the repost is there. */
  async function repost(text: string): Promise<boolean> {
    if (sending.value) return false
    sending.value = true
    sendError.value = null
    try {
      const outcome = await $fetch<RepostOutcome>('/bff/repost', {
        method: 'POST',
        body: { postId: postId(), text },
      }).catch((): RepostOutcome => ({ status: 'error' }))
      if (outcome.status === 'done') {
        reposted.value = true
        await refresh()
        return true
      }
      sendError.value =
        outcome.status === 'refused'
          ? outcome.message
          : outcome.status === 'unauthorized'
            ? t('reposts.signIn')
            : t('reposts.failed')
      return false
    } finally {
      sending.value = false
    }
  }

  return {
    state,
    loadingMore,
    moreFailed,
    loadMore,
    start,
    retry: () => refresh(),
    sending,
    sendError,
    reposted,
    repost,
  }
}
