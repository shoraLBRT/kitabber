// The likes of a post page (E10-01, FR-REACT, ADR-0061): the post's and its comments', read in the browser once the
// page is shown, with one request, and shared by the action panel and the comment tree. A press shows the new mark and
// count at once; the server's answer then replaces them, and a failure puts the old ones back with a toast. A guest's
// press opens the sign-in window (ADR-0032); back on the page signed in, the like is put by itself. While the page is
// open, a signed-in reader gets the new counts of the post and its comments in the realtime topic `post:<id>` (E10-03,
// ADR-0072); the own mark stays as the person's reads and changes left it.
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  commentLikes,
  loadLikes,
  noLikes,
  toggled,
  type LikeChange,
  type LikeState,
  type LikeTarget,
  type PostLikes,
} from '../api/likes'
import {
  likesAfter,
  parseReactionsChanged,
  reactionsChangedType,
  reactionsOfLikes,
  type ReactionCount,
} from '../api/reactions'
import type { RealtimeEvent } from '../realtime/channel'
import { pendingActionWith, tabStorage, takePendingAction } from '../utils/pending-action'
import { t } from '../i18n'

const failure = t('likes.failed')
const signedOut = t('likes.signedOut')

/** The action a guest's press remembers for the sign-in window: `like:<post id>:post` or `like:<post id>:<comment id>`. */
export function likeAction(postId: string, target: LikeTarget, id: string): string {
  return `like:${postId}:${target === 'post' ? 'post' : id}`
}

// The reads and changes of a signed-in person go through the site's own server, which holds the access token
// (server/routes/bff/likes.get.ts, like.post.ts); a guest reads the API directly.
async function readOwn(postId: string): Promise<PostLikes | null> {
  try {
    return await $fetch<PostLikes>('/bff/likes', { query: { postId } })
  } catch {
    return null
  }
}

async function send(target: LikeTarget, id: string, liked: boolean): Promise<LikeChange> {
  try {
    return await $fetch<LikeChange>('/bff/like', { method: 'POST', body: { target, id, liked } })
  } catch {
    return { status: 'error' }
  }
}

/** The likes of the published post `postId`; `null` while there is none (loading, a draft). */
export function useLikes(postId: () => string | null) {
  const { $api } = useNuxtApp()
  const account = useAccount()
  const requireAccount = useRequireAccount()
  const route = useRoute()
  const toast = useToast()
  const channel = useRealtime()
  const post = ref<LikeState>(noLikes)
  const comments = ref<Record<string, LikeState>>({})
  /** The targets whose change the server has not answered yet: `post` or a comment id. */
  const busy = ref<Record<string, true>>({})
  let reads = 0

  async function load(id: string) {
    const current = ++reads
    const found = (account.value.signedIn ? await readOwn(id) : null) ?? (await loadLikes($api, id))
    // A failed read leaves the counts at nothing: the buttons still work, and the server's answer brings the count.
    if (current !== reads || !found) return
    post.value = found.post
    comments.value = commentLikes(found)
  }

  function of(target: LikeTarget, id: string): LikeState {
    return target === 'post' ? post.value : (comments.value[id] ?? noLikes)
  }

  function put(target: LikeTarget, id: string, like: LikeState) {
    if (target === 'post') post.value = like
    else comments.value = { ...comments.value, [id]: like }
  }

  const keyOf = (target: LikeTarget, id: string) => (target === 'post' ? 'post' : id)

  /** Sets the person's mark on the target to `liked`; nothing while its last change is not answered. */
  async function set(target: LikeTarget, id: string, liked: boolean) {
    const key = keyOf(target, id)
    const before = of(target, id)
    if (busy.value[key] || before.liked === liked) return
    busy.value = { ...busy.value, [key]: true }
    // A read answered meanwhile must not draw the old count over this change.
    reads++
    put(target, id, toggled(before))
    const result = await send(target, id, liked)
    busy.value = Object.fromEntries(Object.entries(busy.value).filter(([k]) => k !== key))
    if (result.status === 'done') {
      put(target, id, result.state)
      return
    }
    put(target, id, before)
    toast.show(
      result.status === 'refused' ? result.message : result.status === 'unauthorized' ? signedOut : failure,
      { tone: 'error' },
    )
  }

  /** The reactions of the target for its row (ReactionRow); only likes so far. */
  function reactions(target: LikeTarget, id: string): ReactionCount[] {
    return reactionsOfLikes(of(target, id))
  }

  // New counts from another reader's change, or the echo of one's own. While the person's own change of the target is
  // not answered, the answer brings the count; the mark is never taken from the event, which names no one.
  function receive(event: RealtimeEvent) {
    if (event.type !== reactionsChangedType) return
    const change = parseReactionsChanged(event.data)
    if (!change || (change.target === 'post' && change.targetId !== postId())) return
    if (busy.value[keyOf(change.target, change.targetId)]) return
    put(change.target, change.targetId, likesAfter(of(change.target, change.targetId), change))
  }

  let topic: string | null = null
  let stopListening: (() => void) | null = null

  // One post's topic at a time; a guest and the server have no channel and simply get no live counts.
  function listen(id: string | null) {
    stopListening?.()
    stopListening = null
    if (channel && topic) void channel.unsubscribe(topic)
    topic = id === null ? null : `post:${id}`
    if (!channel || topic === null) return
    stopListening = channel.on(receive, topic)
    void channel.subscribe(topic)
  }

  /** A press on «Нравится»: the mark turns over for a signed-in person, a guest gets the sign-in window. */
  function toggle(target: LikeTarget, id: string) {
    const current = postId()
    if (current === null) return
    requireAccount(likeAction(current, target, id), () => void set(target, id, !of(target, id).liked))
  }

  function isBusy(target: LikeTarget, id: string): boolean {
    return busy.value[keyOf(target, id)] === true
  }

  // The like a guest pressed before the sign-in: put once the counts are read, so the read does not draw over it.
  async function resume(id: string) {
    if (!account.value.signedIn) return
    const prefix = `like:${id}:`
    const action = pendingActionWith(tabStorage(), prefix)
    if (!action || !takePendingAction(tabStorage(), action, route.fullPath)) return
    const target = action.slice(prefix.length)
    await set(target === 'post' ? 'post' : 'comment', target === 'post' ? id : target, true)
  }

  onMounted(() => {
    watch(
      postId,
      async (id) => {
        post.value = noLikes
        comments.value = {}
        listen(id)
        if (id === null) return
        await load(id)
        await resume(id)
      },
      { immediate: true },
    )
  })

  onBeforeUnmount(() => listen(null))

  return { post, comments, of, reactions, toggle, isBusy }
}

export type Likes = ReturnType<typeof useLikes>
