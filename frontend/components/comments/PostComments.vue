<script setup lang="ts">
// The comments of a published post (E09-02, E09-03, E09-04, E09-05), read, sent, ordered, changed and deleted by
// useComments and drawn by CommentTree. A guest's
// press on a form opens the sign-in window (ADR-0032); back on the page signed in, the same form opens. With
// `branchId` it is one branch on its own screen (E09-06); with `postPath`, on a phone, a branch deeper than one read
// opens on such a screen. A link to one comment brings it into view and highlights it (E09-07, ADR-0107).
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
  type CommentPlace,
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

async function reveal(id: string) {
  let place: CommentPlace | null | undefined
  for (let read = 0; !shown(id) && read < revealReads; read++) {
    const current = state.value
    if (current.status !== 'found' || current.comments.length === 0) return
    place ??= await loadCommentPlace($api, id)
    if (place === null || place.postId !== props.postId) return
    if (phone.value && props.postPath !== null) {
      const root = branchRootOf(place, commentLevels.phone)
      if (root !== props.branchId)
        await navigateTo(`${branchPath(props.postPath, root)}${commentHash(id)}`, { replace: true })
      return
    }
    const from = nearestShown(current.comments, place)
    if (from === null) return
    await more(from)
    if (branches.value[from] === 'error') return
  }
  if (!shown(id)) return
  highlighted.value = id
  await nextTick()
  document.getElementById(`comment-${id}`)?.closest('li')?.scrollIntoView({ block: 'start' })
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
    @retry="retry()"
    @sort="order"
    @more="more"
    @send="send"
    @dismiss="dismiss"
    @sign-in="signIn"
    @blocked="blocked"
  />
</template>
