<script setup lang="ts">
// The banner «Показать новые» (E08-08, FR-FEED, docs/ui/feed.md, ADR-0108) above a chronological feed. It takes no room
// of its own: the place stays empty while there is nothing new, and the banner floats over the cards under the header
// when there is, so the feed never moves. Pressing it asks the page to put the new posts on top and scroll up.
import { t } from '../../i18n'

defineProps<{
  /** How many new posts there are; `0` shows nothing. */
  count: number
  /** The whole first page is new, so there may be more than `count`. */
  more?: boolean
}>()

defineEmits<{ show: [] }>()
</script>

<template>
  <div class="new-posts" aria-live="polite" data-testid="new-posts">
    <button
      v-if="count > 0"
      type="button"
      class="new-posts__button"
      data-testid="new-posts-show"
      @click="$emit('show')"
    >
      {{ t('feed.showNew') }}
      <span class="new-posts__count" aria-hidden="true">{{ more ? `${count}+` : count }}</span>
      <span class="visually-hidden">{{ t(more ? 'feed.showNewMore' : 'feed.showNewCount', { count }) }}</span>
    </button>
  </div>
</template>

<style scoped>
.new-posts {
  position: sticky;
  top: calc(var(--space-16) + env(safe-area-inset-top) + var(--space-3));
  z-index: 5;
  display: flex;
  justify-content: center;
  height: 0;
}

.new-posts__button {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: var(--size-touch);
  padding: 0 var(--space-5);
  border: var(--size-border) solid var(--color-accent-hover);
  border-radius: var(--radius-full);
  background: var(--color-accent);
  color: var(--color-on-accent);
  font: inherit;
  font-weight: var(--font-weight-medium);
  white-space: nowrap;
  cursor: pointer;
}

.new-posts__button:hover {
  background: var(--color-accent-hover);
}

.new-posts__button:focus-visible {
  outline-offset: var(--size-focus-ring);
}

.new-posts__count {
  font-size: var(--font-size-sm);
}
</style>
