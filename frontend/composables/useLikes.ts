// The reactions of a post page (E10-01, E10-02, FR-REACT, ADR-0061, ADR-0101): the post's and its comments', read in
// the browser once the page is shown, with one request, and shared by the action panel and the comment tree. A person
// has one reaction on a target: a press on a chip puts that kind or, when it is the person's own, takes it back;
// choosing another kind from the set changes it in one request. The new state shows at once; the server's answer then
// replaces it, and a failure puts the old one back with a toast. A guest's press opens the sign-in window (ADR-0032);
// back on the page signed in, the reaction is put by itself. While the page is open, a signed-in reader gets the new
// counts of the post and its comments in the realtime topic `post:<id>` (E10-03, ADR-0072); the own reaction stays as
// the person's reads and changes left it. The set to choose from (ADR-0095) is read the first time it is opened.
// (The name stays from E10-01, when the like was the only reaction.)
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  chosen,
  isReactionKind,
  loadReactions,
  loadReactionSet,
  noReactions,
  postReactions,
  reactionsAfter,
  parseReactionsChanged,
  reactionsChangedType,
  rowOf,
  type PostReactions,
  type ReactionChange,
  type ReactionCount,
  type ReactionKind,
  type ReactionState,
  type ReactionTarget,
} from '../api/reactions'
import type { ReactionSetState } from '../components/reactions/ReactionSet.vue'
import type { RealtimeEvent } from '../realtime/channel'
import { pendingActionWith, tabStorage, takePendingAction } from '../utils/pending-action'
import { t } from '../i18n'

const failure = t('reactions.failed')
const signedOut = t('reactions.signedOut')

/**
 * The action a guest's press remembers for the sign-in window: `like:<post id>:<post|comment id>:<kind>` (the prefix
 * stays from E10-01).
 */
export function likeAction(
  postId: string,
  target: ReactionTarget,
  id: string,
  kind: ReactionKind = 'like',
): string {
  return `like:${postId}:${target === 'post' ? 'post' : id}:${kind}`
}

// The reads and changes of a signed-in person go through the site's own server, which holds the access token
// (server/routes/bff/reactions.get.ts, reaction.post.ts); a guest reads the API directly.
async function readOwn(postId: string): Promise<PostReactions | null> {
  try {
    return await $fetch<PostReactions>('/bff/reactions', { query: { postId } })
  } catch {
    return null
  }
}

async function send(target: ReactionTarget, id: string, kind: ReactionKind | null): Promise<ReactionChange> {
  try {
    return await $fetch<ReactionChange>('/bff/reaction', { method: 'POST', body: { target, id, kind } })
  } catch {
    return { status: 'error' }
  }
}

/** The reactions of the published post `postId`; `null` while there is none (loading, a draft). */
export function useLikes(postId: () => string | null) {
  const { $api } = useNuxtApp()
  const account = useAccount()
  const requireAccount = useRequireAccount()
  const route = useRoute()
  const toast = useToast()
  const channel = useRealtime()
  const post = ref<ReactionState>(noReactions)
  const comments = ref<Record<string, ReactionState>>({})
  /** The set of reactions to choose from; read the first time a row opens it. */
  const set = ref<ReactionSetState | null>(null)
  /** The targets whose change the server has not answered yet: `post` or a comment id. */
  const busy = ref<Record<string, true>>({})
  let reads = 0

  async function load(id: string) {
    const current = ++reads
    const found = (account.value.signedIn ? await readOwn(id) : null) ?? (await loadReactions($api, id))
    // A failed read leaves the counts at nothing: the buttons still work, and the server's answer brings the counts.
    if (current !== reads || !found) return
    const read = postReactions(found)
    post.value = read.post
    comments.value = read.comments
  }

  /** Reads the set of reactions, once, or again after a failure. */
  async function loadSet() {
    if (set.value && set.value.status !== 'error') return
    set.value = { status: 'loading' }
    const found = await loadReactionSet($api)
    set.value = found.status === 'found' ? found : { status: 'error' }
  }

  function of(target: ReactionTarget, id: string): ReactionState {
    return target === 'post' ? post.value : (comments.value[id] ?? noReactions)
  }

  function put(target: ReactionTarget, id: string, state: ReactionState) {
    if (target === 'post') post.value = state
    else comments.value = { ...comments.value, [id]: state }
  }

  const keyOf = (target: ReactionTarget, id: string) => (target === 'post' ? 'post' : id)

  /** Makes the person's reaction on the target `kind` (`null`: none); nothing while its last change is not answered. */
  async function change(target: ReactionTarget, id: string, kind: ReactionKind | null) {
    const key = keyOf(target, id)
    const before = of(target, id)
    if (busy.value[key] || before.mine === kind) return
    busy.value = { ...busy.value, [key]: true }
    // A read answered meanwhile must not draw the old counts over this change.
    reads++
    put(target, id, chosen(before, kind))
    const result = await send(target, id, kind)
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

  /** The reactions of the target for its row (ReactionRow). */
  function reactions(target: ReactionTarget, id: string): ReactionCount[] {
    return rowOf(of(target, id))
  }

  // New counts from another reader's change, or the echo of one's own. While the person's own change of the target is
  // not answered, the answer brings the counts; the own reaction is never taken from the event, which names no one.
  function receive(event: RealtimeEvent) {
    if (event.type !== reactionsChangedType) return
    const change = parseReactionsChanged(event.data)
    if (!change || (change.target === 'post' && change.targetId !== postId())) return
    if (busy.value[keyOf(change.target, change.targetId)]) return
    put(change.target, change.targetId, reactionsAfter(of(change.target, change.targetId), change))
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

  /**
   * A choice of `kind` on the target: the person's own kind is taken back, any other one becomes theirs (put or
   * changed); a guest gets the sign-in window.
   */
  function choose(target: ReactionTarget, id: string, kind: ReactionKind) {
    const current = postId()
    if (current === null) return
    requireAccount(likeAction(current, target, id, kind), () => {
      void change(target, id, of(target, id).mine === kind ? null : kind)
    })
  }

  /** A press on «Нравится»: the like is put, or the person's reaction, whatever it is, is taken back. */
  function toggle(target: ReactionTarget, id: string) {
    choose(target, id, of(target, id).mine ?? 'like')
  }

  function isBusy(target: ReactionTarget, id: string): boolean {
    return busy.value[keyOf(target, id)] === true
  }

  // The reaction a guest chose before the sign-in: put once the counts are read, so the read does not draw over it.
  async function resume(id: string) {
    if (!account.value.signedIn) return
    const prefix = `like:${id}:`
    const action = pendingActionWith(tabStorage(), prefix)
    if (!action || !takePendingAction(tabStorage(), action, route.fullPath)) return
    const [target = '', kind] = action.slice(prefix.length).split(':')
    await change(
      target === 'post' ? 'post' : 'comment',
      target === 'post' ? id : target,
      isReactionKind(kind) ? kind : 'like',
    )
  }

  onMounted(() => {
    watch(
      postId,
      async (id) => {
        post.value = noReactions
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

  return { post, comments, set, of, reactions, choose, toggle, loadSet, isBusy }
}

export type Likes = ReturnType<typeof useLikes>
