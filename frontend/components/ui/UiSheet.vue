<script setup lang="ts">
// Bottom sheet (E02-04, ADR-0013): the modal dialog presented as a sheet at every width — the same focus trap,
// Escape, backdrop and focus return as UiModal.
import UiModal from './UiModal.vue'

withDefaults(defineProps<{ title: string; closeLabel?: string }>(), { closeLabel: 'Закрыть' })

const open = defineModel<boolean>('open', { default: false })
</script>

<template>
  <UiModal v-model:open="open" :title="title" :close-label="closeLabel" presentation="sheet">
    <slot />
    <template v-if="$slots.footer" #footer><slot name="footer" /></template>
  </UiModal>
</template>
