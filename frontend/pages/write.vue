<script setup lang="ts">
// New post (E05-01, E05-09, docs/ui/editor.md): the editor window over the shell with the title, the topic and
// «Опубликовать» (components/editor/PostEditor.vue). The topics and publishing go through the site's server
// (server/routes/bff/topics.get.ts, publish.post.ts, ADR-0018). A published post opens on its page; a guest who
// publishes gets the sign-in window (ADR-0032) — until drafts with autosave come (their own task), the text is not
// kept across the sign-in. Closing goes back.
import PostEditor from '~/components/editor/PostEditor.vue'
import type { PublishInput, PublishOutcome } from '~/api/publish'
import type { Topic } from '~/api/topics'

useHead({ title: 'Новый пост' })

const router = useRouter()
const account = useAccount()
const authWindow = useAuthWindow()
const { data: topics } = await useAsyncData('topics', () => $fetch<Topic[]>('/bff/topics').catch(() => []), {
  lazy: true,
  default: () => [],
})

const publish = async (input: PublishInput): Promise<PublishOutcome> =>
  !account.value.signedIn
    ? { status: 'unauthorized' }
    : $fetch<PublishOutcome>('/bff/publish', { method: 'POST', body: input }).catch(
        (error: { statusCode?: number }): PublishOutcome =>
          error.statusCode === 401 ? { status: 'unauthorized' } : { status: 'error', draftId: input.draftId },
      )

function close() {
  if (window.history.state?.back) router.back()
  else void navigateTo('/')
}

function onPublished(path: string) {
  // The feeds ask again when they open next, so the new post is there.
  clearNuxtData((key) => key.startsWith('feed'))
  void navigateTo(path, { replace: true })
}

function onUnauthorized() {
  authWindow.value = { open: true, mode: 'sign-in' }
}
</script>

<template>
  <PostEditor
    :topics="topics"
    :publish="publish"
    @close="close"
    @published="onPublished"
    @unauthorized="onUnauthorized"
  />
</template>
