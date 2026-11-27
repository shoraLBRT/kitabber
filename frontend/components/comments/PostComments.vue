<script setup lang="ts">
// The comments of a published post (E09-02, E09-03, E09-04, E09-05), read, sent, ordered, changed and deleted by
// useComments and drawn by CommentTree. A guest's
// press on a form opens the sign-in window (ADR-0032); back on the page signed in, the same form opens. With
// `branchId` it is one branch on its own screen (E09-06); with `postPath`, on a phone, a branch deeper than one read
// opens on such a screen. A link to one comment brings it into view and highlights it (E09-07, ADR-0107). For a
// signed-in reader the comments new since the previous opening are marked, and «К следующему новому» goes through them
// (E09-08, ADR-0109). Those others write meanwhile wait behind «N новых» and join them once shown (E09-09, ADR-0112).
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import CommentTree from './CommentTree.vue'
import type { FormTarget } from './context'
import {
  branchPath,
  branchRootOf,
  commentHash,
  commentLevels,
  commentOfHash,
  commentPath,
  loadCommentPlace,
  nearestShown,
  newCommentsOrder,
  noVisit,
  type CommentPlace,
  type CommentVisit,
} from '../../api/comments'
import { useComments } from '../../composables/useComments'
import { useLikes, type Likes } from '../../composables/useLikes'
import { pendingActionWith, tabStorage, takePendingAction } from '../../utils/pending-action'

const props = withDefaults(
  defineProps<{
    postId: string
    postPath?: string | null
    branchId?: string | null
    /** The likes of the post page (E10-01), shared with its action panel; a branch's own screen reads its own. */
    likes?: Likes | null
  }>(),
  { postPath: null, branchId: null, likes: null },
)

const likes = props.likes ?? useLikes(() => props.postId)

const {
  state,
  branches,
  authors,
  pending,
  failures,
  own,
  ownRead,
  sort,
  phone,
  retry,
  more,
  send,
  edit,
  remove,
  dismiss,
  order,
  blocked,
  arrived,
  live,
  showArrived,
} = useComments(
  () => props.postId,
  () => props.branchId,
)
const branchLink = computed(() => {
  const postPath = props.postPath
  return phone.value && postPath !== null ? (commentId: string) => branchPath(postPath, commentId) : null
})
const { $api } = useNuxtApp()
const account = useAccount()
const authWindow = useAuthWindow()
const route = useRoute()
const resume = ref<FormTarget | null>(null)

const actionPrefix = () => `comment:${props.postId}:`

function signIn(target: FormTarget) {
  authWindow.value = { open: true, mode: 'sign-in', action: `${actionPrefix()}${target}` }
}

// A link to one comment (`#comment-<id>`, E09-07, ADR-0107; the best comment of a feed card, ADR-0062): once the tree
// is read, the comment comes into view and is highlighted. One deeper than the read comes with its branch: on a wide
// screen the branch is read in place from the deepest of its ancestors shown, on a phone the branch's screen opens
// with the comment within its read (ADR-0060). Only the first read counts, so a later change of order or a reply does
// not pull the page back; a new link on the same page (the time of a comment, the widget) is followed at once.
const highlighted = ref<string | null>(null)
const commentLink = computed(() => {
  const postPath = props.postPath
  return postPath === null ? null : (commentId: string) => commentPath(postPath, commentId)
})
// The reads of the rest of a branch a link may take: a branch is at most `maxDepth` levels, read some at a time.
const revealReads = 8
let target = import.meta.client ? commentOfHash(route.hash) : null

const shown = (id: string) => state.value.status === 'found' && state.value.comments.some((c) => c.id === id)

/** Reads the branch that holds the comment `id` until it is shown: `false` when it is not (or a phone left for it). */
async function bring(id: string): Promise<boolean> {
  let place: CommentPlace | null | undefined
  for (let read = 0; !shown(id) && read < revealReads; read++) {
    const current = state.value
    if (current.status !== 'found' || current.comments.length === 0) return false
    place ??= await loadCommentPlace($api, id)
    if (place === null || place.postId !== props.postId) return false
    if (phone.value && props.postPath !== null) {
      const root = branchRootOf(place, commentLevels.phone)
      if (root !== props.branchId)
        await navigateTo(`${branchPath(props.postPath, root)}${commentHash(id)}`, { replace: true })
      return false
    }
    const from = nearestShown(current.comments, place)
    if (from === null) return false
    await more(from)
    if (branches.value[from] === 'error') return false
  }
  return shown(id)
}

async function scrollTo(id: string) {
  await nextTick()
  document.getElementById(`comment-${id}`)?.closest('li')?.scrollIntoView({ block: 'start' })
}

async function reveal(id: string) {
  if (!(await bring(id))) return
  highlighted.value = id
  await scrollTo(id)
}

// The comments new since the previous opening (E09-08, ADR-0109): the opening is asked once the post's tree is on
// screen, through the site's server, and moves on to now. Coming back to the post within a few minutes (a phone back
// from a branch's screen) keeps the same answer, so the marks are not lost on the way.
const visitReuse = 10 * 60_000
const visit = useState<{ at: number; visit: CommentVisit } | null>(
  `comment-visit:${props.postId}`,
  () => null,
)
// The comments that came in real time and were put in the tree (E09-09, ADR-0112) are new as well.
const newIds = computed(() => [...(visit.value?.visit.newIds ?? []), ...live.value])
const fresh = computed(() => Object.fromEntries(newIds.value.map((id) => [id, true as const])))
const gone = ref<string[]>([])
const freshOrder = computed(() =>
  state.value.status === 'found'
    ? newCommentsOrder(state.value.comments, newIds.value, new Set(gone.value), !phone.value)
    : [],
)

async function openVisit() {
  if (!account.value.signedIn || props.branchId !== null) return
  const kept = visit.value
  if (kept && Date.now() - kept.at < visitReuse) return
  let answer = noVisit
  try {
    answer = await $fetch<CommentVisit>('/bff/comment-visit', {
      method: 'POST',
      body: { postId: props.postId },
    })
  } catch {
    // No answer, nothing marked: the comments read as before.
  }
  visit.value = { at: Date.now(), visit: answer }
}

/** «К следующему новому»: the first new comment not gone to yet, in the order shown, read in place when deeper. */
async function nextNew() {
  const id = freshOrder.value[0]
  if (id === undefined) return
  gone.value = [...gone.value, id]
  if (await bring(id)) await scrollTo(id)
}

watch(
  state,
  (next) => {
    if (target === null || next.status === 'loading') return
    const id = target
    target = null
    if (next.status === 'found') void reveal(id)
  },
  { immediate: true },
)
watch(
  () => route.hash,
  (hash) => {
    const id = commentOfHash(hash)
    if (id === null) return
    if (state.value.status === 'loading') target = id
    else void reveal(id)
  },
)

onMounted(() => {
  void openVisit()
  if (!account.value.signedIn) return
  const action = pendingActionWith(tabStorage(), actionPrefix())
  if (action && takePendingAction(tabStorage(), action, route.fullPath)) {
    resume.value = action.slice(actionPrefix().length)
  }
})
</script>

<template>
  <CommentTree
    :post-id="postId"
    :signed-in="account.signedIn"
    :state="state"
    :authors="authors"
    :branches="branches"
    :pending="pending"
    :failures="failures"
    :resume="resume"
    :sort="sort"
    :own="own"
    :edit="edit"
    :remove="remove"
    :branch-link="branchLink"
    :branch="branchId !== null"
    :likes="likes"
    :can-block="account.signedIn && ownRead"
    :highlighted="highlighted"
    :comment-link="commentLink"
    :fresh="fresh"
    :fresh-left="freshOrder.length"
    :arrived="arrived.length"
    @retry="retry()"
    @sort="order"
    @more="more"
    @send="send"
    @dismiss="dismiss"
    @sign-in="signIn"
    @blocked="blocked"
    @next-new="nextNew()"
    @show-arrived="showArrived()"
  />
</template>
