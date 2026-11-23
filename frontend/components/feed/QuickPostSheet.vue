<script setup lang="ts">
// The quick post on a phone (E05-29, ADR-0055): the floating «Написать» opens it as a bottom sheet over the page
// (useQuickPost().sheetOpen). One sheet per page, in app.vue; it is rendered from its first opening on, never on the
// server, as the sign-in window is (AuthWindowHost), so an empty teleport does not upset the hydration.
import QuickPost from './QuickPost.vue'
import UiSheet from '../ui/UiSheet.vue'

const { sheetOpen } = useQuickPost()
const rendered = ref(false)
watch(
  sheetOpen,
  (isOpen) => {
    if (isOpen) rendered.value = true
  },
  { immediate: true },
)
</script>

<template>
  <UiSheet v-if="rendered" v-model:open="sheetOpen" title="Новый пост">
    <QuickPost @done="sheetOpen = false" />
  </UiSheet>
</template>
