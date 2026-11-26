<script setup lang="ts">
// New post (E05-01, E05-09, docs/ui/editor.md): the editor window over the shell with the title, the topic and
// «Опубликовать» (components/editor/PostEditor.vue). The topics and publishing go through the site's server
// (composables/usePostPublishing.ts, ADR-0018). A published post opens on its page; a guest who publishes gets the
// sign-in window (ADR-0032) — until drafts with autosave come (their own task), the text is not kept across the
// sign-in. Opened by «Развернуть» of the quick post (E05-29, ADR-0055), the editor starts from its text, photos and
// topic. Closing goes back.
import PostEditor from '~/components/editor/PostEditor.vue'
import { t } from '../i18n'

useHead({ title: t('pages.write') })

const router = useRouter()
const authWindow = useAuthWindow()
const { publish, uploadImage, openPublished } = usePostPublishing()
const { data: topics } = await useTopics()

// The quick post's hand-off is taken once: coming back to /write later starts empty.
const { handoff } = useQuickPost()
const initial = handoff.value
handoff.value = null

function close() {
  if (window.history.state?.back) router.back()
  else void navigateTo('/')
}

function onUnauthorized() {
  authWindow.value = { open: true, mode: 'sign-in' }
}
</script>

<template>
  <PostEditor
    :topics="topics"
    :publish="publish"
    :upload-image="uploadImage"
    :initial="initial"
    @close="close"
    @published="openPublished($event, true)"
    @unauthorized="onUnauthorized"
  />
</template>
