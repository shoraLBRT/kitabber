<script setup lang="ts">
// A message in place of the content (E02-07, FR-UI, docs/ui/README.md, ADR-0057), one look for every screen:
//   `empty`  nothing to show yet: a quiet text, and an action when there is one to take;
//   `error`  the content did not load: the text `--color-text` with the icon `--color-danger`, told to screen readers
//            at once (`role="alert"`), and «Повторить» in the `actions` slot;
//   `not-found` / `forbidden`  a page that is not there or is closed, with a link to the main page in `actions`.
// The title is optional; a page of its own (404, 500) makes it the page's `h1`.
import { CircleAlert } from 'lucide-vue-next'

withDefaults(
  defineProps<{
    tone?: 'empty' | 'error' | 'not-found' | 'forbidden'
    title?: string
    /** The level of the title: `h1` on a page of its own, `h2` inside a page. */
    heading?: 'h1' | 'h2'
  }>(),
  { tone: 'empty', title: undefined, heading: 'h2' },
)
</script>

<template>
  <section :class="['ui-state', `ui-state--${tone}`]" :data-tone="tone" data-testid="state">
    <component :is="heading" v-if="title" class="ui-state__title">{{ title }}</component>
    <p class="ui-state__text" :role="tone === 'error' ? 'alert' : undefined">
      <CircleAlert v-if="tone === 'error'" class="ui-state__icon" aria-hidden="true" />
      <span><slot /></span>
    </p>
    <div v-if="$slots.actions" class="ui-state__actions">
      <slot name="actions" />
    </div>
  </section>
</template>

<style scoped>
.ui-state {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-3);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.ui-state__title {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.ui-state__text {
  display: flex;
  align-items: flex-start;
  gap: var(--space-2);
  margin: 0;
  color: var(--color-muted);
}

.ui-state--error .ui-state__text {
  color: var(--color-text);
}

.ui-state__icon {
  flex-shrink: 0;
  width: var(--space-6);
  height: var(--space-6);
  color: var(--color-danger);
}

.ui-state__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
}

@media (max-width: 639px) {
  .ui-state {
    border-radius: 0;
  }
}
</style>
