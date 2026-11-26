<script setup lang="ts">
// The comments of a published post (E09-02, E09-03, E09-04, E09-05), read, sent, ordered, changed and deleted by
// useComments and drawn by CommentTree. A guest's
// press on a form opens the sign-in window (ADR-0032); back on the page signed in, the same form opens. With
// `branchId` it is one branch on its own screen (E09-06); with `postPath`, on a phone, a branch deeper than one read
// opens on such a screen.
import { computed, onMounted, ref } from 'vue'
import CommentTree from './CommentTree.vue'
import type { FormTarget } from './context'
import { branchPath } from '../../api/comments'
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
  sort,
  phone,
  retry,
  more,
  send,
  edit,
  remove,
  dismiss,
  order,
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
    @retry="retry()"
    @sort="order"
    @more="more"
    @send="send"
    @dismiss="dismiss"
    @sign-in="signIn"
  />
</template>
