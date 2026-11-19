<script setup lang="ts">
// Avatar (E02-04, ADR-0013): the picture, or the initials when there is none or it fails to load. Its accessible
// name is the person's name; next to a visible name pass decorative to hide it from screen readers.
import { computed, onMounted, ref, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    name: string
    src?: string
    size?: 'sm' | 'md' | 'lg'
    decorative?: boolean
  }>(),
  { src: undefined, size: 'md' },
)

const failed = ref(false)
const img = ref<HTMLImageElement>()
// A picture rendered on the server may fail before hydration, when @error is not listened to yet.
onMounted(() => {
  if (img.value?.complete && img.value.naturalWidth === 0) failed.value = true
})
watch(
  () => props.src,
  () => {
    failed.value = false
  },
)

const initials = computed(() => {
  const letters = props.name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => [...word][0]!.toLocaleUpperCase('ru'))
  return letters.join('') || '?'
})
</script>

<template>
  <img
    v-if="src && !failed"
    ref="img"
    class="ui-avatar"
    :class="`ui-avatar--${size}`"
    :src="src"
    :alt="decorative ? '' : name"
    :aria-hidden="decorative ? 'true' : undefined"
    @error="failed = true"
  />
  <span
    v-else
    class="ui-avatar ui-avatar--initials"
    :class="`ui-avatar--${size}`"
    :role="decorative ? undefined : 'img'"
    :aria-label="decorative ? undefined : name"
    :aria-hidden="decorative ? 'true' : undefined"
  >
    <span aria-hidden="true">{{ initials }}</span>
  </span>
</template>

<style scoped>
.ui-avatar {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: var(--space-10);
  height: var(--space-10);
  border-radius: var(--radius-full);
  object-fit: cover;
}

.ui-avatar--sm {
  width: var(--space-8);
  height: var(--space-8);
  font-size: var(--font-size-xs);
}

.ui-avatar--lg {
  width: var(--space-16);
  height: var(--space-16);
  font-size: var(--font-size-xl);
}

.ui-avatar--initials {
  background: var(--color-surface-subtle);
  color: var(--color-muted);
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-tight);
  user-select: none;
}
</style>
