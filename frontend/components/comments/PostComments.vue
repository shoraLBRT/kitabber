<script setup lang="ts">
// The comments of a published post (E09-02, E09-03), read and sent by useComments and drawn by CommentTree. A guest's
// press on a form opens the sign-in window (ADR-0032); back on the page signed in, the same form opens.
import { onMounted, ref } from 'vue'
import CommentTree from './CommentTree.vue'
import type { FormTarget } from './context'
import { useComments } from '../../composables/useComments'
import { pendingActionWith, tabStorage, takePendingAction } from '../../utils/pending-action'

const props = defineProps<{ postId: string }>()

const { state, branches, authors, pending, failures, retry, more, send, dismiss } = useComments(
  () => props.postId,
)
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
    @retry="retry()"
    @more="more"
    @send="send"
    @dismiss="dismiss"
    @sign-in="signIn"
  />
</template>
