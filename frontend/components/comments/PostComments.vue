<script setup lang="ts">
// The comments of a published post (E09-02, E09-03, E09-04, E09-05), read, sent, ordered, changed and deleted by
// useComments and drawn by CommentTree. A guest's
// press on a form opens the sign-in window (ADR-0032); back on the page signed in, the same form opens. With
// `branchId` it is one branch on its own screen (E09-06); with `postPath`, on a phone, a branch deeper than one read
// opens on such a screen.
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import CommentTree from './CommentTree.vue'
import type { FormTarget } from './context'
import { branchPath, commentOfHash } from '../../api/comments'
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
const account = useAccount()
const authWindow = useAuthWindow()
const route = useRoute()
const resume = ref<FormTarget | null>(null)

const actionPrefix = () => `comment:${props.postId}:`

function signIn(target: FormTarget) {
  authWindow.value = { open: true, mode: 'sign-in', action: `${actionPrefix()}${target}` }
}

// A link to one comment (`#comment-<id>`, the best comment of a feed card, E08-15, ADR-0062): once the tree is read,
// the comment comes into view; one deeper than the read opens on its branch's screen. Only the first read counts, so
// a later change of order or a reply does not pull the page back.
let target = props.branchId === null && import.meta.client ? commentOfHash(route.hash) : null
watch(
  state,
  async (next) => {
    if (target === null || next.status === 'loading') return
    const id = target
    target = null
    if (next.status !== 'found') return
    if (next.comments.some((comment) => comment.id === id)) {
      await nextTick()
      document.getElementById(`comment-${id}`)?.closest('li')?.scrollIntoView({ block: 'start' })
    } else if (next.comments.length > 0 && props.postPath !== null) {
      await navigateTo(branchPath(props.postPath, id), { replace: true })
    }
  },
  { immediate: true },
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
    @retry="retry()"
    @sort="order"
    @more="more"
    @send="send"
    @dismiss="dismiss"
    @sign-in="signIn"
    @blocked="blocked"
  />
</template>
