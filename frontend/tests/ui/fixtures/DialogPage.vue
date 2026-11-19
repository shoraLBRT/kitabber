<script setup lang="ts">
// Test page for UiModal and UiSheet: an opener button and a dialog with two buttons of its own.
import { ref, type Component } from 'vue'

defineProps<{ dialog: Component; presentation?: 'auto' | 'modal' | 'sheet' }>()
const open = ref(false)
</script>

<template>
  <button id="opener" type="button" @click="open = true">Открыть</button>
  <component
    :is="dialog"
    v-model:open="open"
    title="Удалить черновик?"
    v-bind="presentation ? { presentation } : {}"
  >
    <p>Текст</p>
    <template #footer>
      <button id="cancel" type="button">Отмена</button>
      <button id="ok" type="button">Удалить</button>
    </template>
  </component>
</template>
