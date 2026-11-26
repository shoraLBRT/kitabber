<script setup lang="ts">
// Avatar (E02-04, ADR-0013): the picture, or the initials when there is none or it fails to load. Its accessible
// name is the person's name; next to a visible name pass decorative to hide it from screen readers. The picture is a
// UiImage (E06-04, ADR-0040): with sources the browser takes the width that fits the avatar's size at the screen's
// density.
import { computed, ref, watch } from 'vue'
import type { ImageSource } from '../../types/ui'
import UiImage from './UiImage.vue'
import { locale } from '../../i18n'

const props = withDefaults(
  defineProps<{
    name: string
    src?: string
    sources?: readonly ImageSource[]
    size?: 'sm' | 'md' | 'lg'
    decorative?: boolean
  }>(),
  { src: undefined, sources: () => [], size: 'md' },
)

// The CSS width of each size (--space-8, --space-10, --space-16), the sizes attribute of the picture.
const pixels = { sm: 32, md: 40, lg: 64 } as const

const failed = ref(false)
watch(
  () => [props.src, props.sources],
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
    .map((word) => [...word][0]!.toLocaleUpperCase(locale))
  return letters.join('') || '?'
})
</script>

<template>
  <UiImage
    v-if="(src || sources.length > 0) && !failed"
    class="ui-avatar"
    :class="`ui-avatar--${size}`"
    :src="src"
    :sources="sources"
    :sizes="`${pixels[size]}px`"
    :width="pixels[size]"
    :height="pixels[size]"
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
