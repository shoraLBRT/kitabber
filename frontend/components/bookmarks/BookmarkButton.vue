<script setup lang="ts">
// The bookmark button of a post (E10-07, FR-REACT, ADR-0113): «В закладки», pressed while the person keeps the post,
// on the post page's action panel (`panel`) and among a feed card's counters (`card`). Its author sees beside it how
// many people keep the post; nobody else gets that number. A press is passed on (`press`): useBookmarks puts or takes
// out the bookmark, or opens the sign-in window for a guest.
import { computed } from 'vue'
import { Bookmark } from 'lucide-vue-next'
import type { BookmarkState } from '../../api/bookmarks'
import { t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    state: BookmarkState
    busy?: boolean
    variant?: 'panel' | 'card'
    testid?: string
  }>(),
  { busy: false, variant: 'card', testid: 'bookmark' },
)

defineEmits<{ press: [] }>()

const count = computed(() => (props.state.count !== null && props.state.count > 0 ? props.state.count : null))
const counted = computed(() => (count.value === null ? '' : t('bookmarks.count', { count: count.value })))
</script>

<template>
  <button
    type="button"
    :class="['bookmark', `bookmark--${variant}`, { 'bookmark--on': state.bookmarked }]"
    :aria-pressed="state.bookmarked ? 'true' : 'false'"
    :aria-busy="busy ? 'true' : undefined"
    :title="state.bookmarked ? t('bookmarks.remove') : t('bookmarks.add')"
    :data-testid="testid"
    @click="$emit('press')"
  >
    <Bookmark class="bookmark__icon" aria-hidden="true" />
    <span :class="{ 'visually-hidden': variant === 'card' }">{{ t('bookmarks.add') }}</span>
    <template v-if="count !== null">
      <span class="bookmark__count" aria-hidden="true" data-testid="bookmark-count">{{ count }}</span>
      <span class="visually-hidden">{{ counted }}</span>
    </template>
  </button>
</template>

<style scoped>
.bookmark {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-1);
  min-height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-full);
  background: transparent;
  color: inherit;
  font: inherit;
  cursor: pointer;
}

.bookmark--panel {
  gap: var(--space-2);
  padding: 0 var(--space-4);
  color: var(--color-text);
  font-size: var(--font-size-sm);
}

.bookmark--card {
  min-width: var(--size-touch);
  padding: 0 var(--space-2);
  color: var(--color-muted);
}

.bookmark:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.bookmark--on,
.bookmark--on:hover {
  color: var(--color-accent);
}

.bookmark__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
  flex: none;
}

.bookmark--card .bookmark__icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.bookmark--on .bookmark__icon {
  fill: currentcolor;
}

/* Icons only on a phone's action panel, as its other buttons (PostActions); the label stays for screen readers. */
@media (max-width: 639px) {
  .bookmark--panel {
    padding: 0 var(--space-2);
  }

  .bookmark--panel > span:not(.bookmark__count) {
    position: absolute;
    width: var(--size-border);
    height: var(--size-border);
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
}
</style>
