<script setup lang="ts">
// The picture of one reaction (E02-12, ADR-0095): the bundled SVG of its code at a fixed size, the same on every
// system. With `title` it is the reaction's name for screen readers; without it, it is decoration next to a text.
// A code with no picture in this build draws nothing rather than a broken image.
import { computed } from 'vue'
import { reactionGraphic } from '../../utils/reaction-graphics'

const props = withDefaults(defineProps<{ code: string; title?: string; size?: 'sm' | 'md' | 'lg' }>(), {
  title: undefined,
  size: 'md',
})

const src = computed(() => reactionGraphic(props.code))
const pixels = computed(() => ({ sm: 16, md: 24, lg: 40 })[props.size])
</script>

<template>
  <img
    v-if="src"
    class="reaction-icon"
    :src="src"
    :alt="title ?? ''"
    :aria-hidden="title ? undefined : 'true'"
    :width="pixels"
    :height="pixels"
    :data-code="code"
    draggable="false"
    data-testid="reaction-icon"
  />
</template>

<style scoped>
.reaction-icon {
  display: inline-block;
  flex: none;
  vertical-align: middle;
  user-select: none;
}
</style>
