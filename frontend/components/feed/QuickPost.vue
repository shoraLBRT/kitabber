<script setup lang="ts">
// The quick post of the site (E05-29, ADR-0055): the form (components/editor/QuickPostForm.vue) with the site's
// server behind it (composables/usePostPublishing.ts). Above the feed on a desktop and in the sheet of the floating
// «Написать» on a phone. A published post opens on its page; «Развернуть» hands the text, the photos and the topic to
// the full editor at /write; a guest's «Войти» opens the sign-in window. `done` tells the sheet to close.
import QuickPostForm from '../editor/QuickPostForm.vue'
import type { QuickPostHandoff } from '~/editor/quick-post'
import { writePath } from '~/utils/navigation'

const emit = defineEmits<{ done: [] }>()

const { account, publish, uploadImage, openPublished } = usePostPublishing()
const { data: topics } = useTopics()
const { handoff } = useQuickPost()
const authWindow = useAuthWindow()

function onPublished(path: string) {
  emit('done')
  openPublished(path)
}

function onExpand(next: QuickPostHandoff) {
  handoff.value = next
  emit('done')
  void navigateTo(writePath)
}

function signIn() {
  emit('done')
  authWindow.value = { open: true, mode: 'sign-in' }
}
</script>

<template>
  <QuickPostForm
    :topics="topics"
    :signed-in="account.signedIn"
    :publish="publish"
    :upload-image="uploadImage"
    @published="onPublished"
    @expand="onExpand"
    @unauthorized="signIn"
    @sign-in="signIn"
  />
</template>
