<script setup lang="ts">
// A skeleton (E02-07, FR-UI, docs/ui/README.md, ADR-0057): a `--color-surface-subtle` block in the shape of the content
// it stands for, so the page does not jump when the content comes. Its size comes from the same tokens as that content:
//   `text`   one line of text of the font size `size`: the line box of that text, the bar inside it (`reading` is
//            the text of a post, at the reading line height);
//   `avatar` the circle of UiAvatar of the size `size`;
//   `block`  a rectangle of the height `height` (any CSS length), or of the proportions `ratio` of a picture.
// Decorative: the state is told to screen readers by the region that shows the skeletons (`aria-busy` and a status).
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    shape?: 'text' | 'avatar' | 'block'
    /** `text`: the font size token (sm, md, reading, lg, xl, 2xl); `avatar`: sm, md or lg as in UiAvatar. */
    size?: 'sm' | 'md' | 'reading' | 'lg' | 'xl' | '2xl'
    /** The width of a line or a block, a CSS length; the whole width by default. */
    width?: string
    /** `block`: its height, a CSS length. */
    height?: string
    /** `block`: width / height, when the height follows the width as a picture's does. */
    ratio?: number
  }>(),
  { shape: 'text', size: 'md', width: undefined, height: undefined, ratio: undefined },
)

const style = computed(() => ({
  width: props.shape === 'avatar' ? undefined : props.width,
  height: props.shape === 'block' ? props.height : undefined,
  aspectRatio: props.shape === 'block' && props.ratio ? String(props.ratio) : undefined,
}))
</script>

<template>
  <span
    :class="['ui-skeleton', `ui-skeleton--${shape}`, `ui-skeleton--${shape}-${size}`]"
    :style="style"
    aria-hidden="true"
    data-testid="skeleton"
  />
</template>

<style scoped>
.ui-skeleton {
  display: block;
  flex-shrink: 0;
  max-width: 100%;
  background-color: var(--color-surface-subtle);
  animation: ui-skeleton-pulse 1.6s ease-in-out infinite;
}

/* A line takes the line box of its text; the bar is the middle of it, as the letters are. */
.ui-skeleton--text {
  border-radius: var(--radius-sm);
  background: linear-gradient(transparent 20%, var(--color-surface-subtle) 20% 80%, transparent 80%);
  line-height: var(--line-height-normal);
}

.ui-skeleton--text::before {
  content: '\00a0';
}

.ui-skeleton--text-sm {
  font-size: var(--font-size-sm);
}

.ui-skeleton--text-md {
  font-size: var(--font-size-md);
}

/* The text of a post is read at its own line height (PostBlocks). */
.ui-skeleton--text-reading {
  font-size: var(--font-size-reading);
  line-height: var(--line-height-reading);
}

.ui-skeleton--text-lg {
  font-size: var(--font-size-lg);
}

.ui-skeleton--text-xl {
  font-size: var(--font-size-xl);
}

.ui-skeleton--text-2xl {
  font-size: var(--font-size-2xl);
}

/* The sizes of UiAvatar. */
.ui-skeleton--avatar {
  width: var(--space-10);
  height: var(--space-10);
  border-radius: var(--radius-full);
}

.ui-skeleton--avatar-sm {
  width: var(--space-8);
  height: var(--space-8);
}

.ui-skeleton--avatar-lg {
  width: var(--space-16);
  height: var(--space-16);
}

.ui-skeleton--block {
  width: 100%;
  border-radius: var(--radius-md);
}

@keyframes ui-skeleton-pulse {
  50% {
    opacity: 0.5;
  }
}

@media (prefers-reduced-motion: reduce) {
  .ui-skeleton {
    animation: none;
  }
}
</style>
