<script setup lang="ts">
// The rating filter of «Свежее» (E08-07, FR-FEED, ADR-0106): «Все», «От −10», «От +5», «От +10». The chosen threshold
// is pressed; choosing another one asks the page to read the feed again with it. The server applies the threshold, and
// the page remembers it (in the account, or in this browser for a guest). On a phone the row scrolls sideways.
import { freshRatings, freshRatingValue, type FreshRating } from '../../api/feeds'
import { t } from '../../i18n'

defineProps<{ current: FreshRating }>()
const emit = defineEmits<{ choose: [rating: FreshRating] }>()

function label(rating: FreshRating): string {
  return rating === null
    ? t('feed.freshRatingAll')
    : t('feed.freshRatingFrom', { value: freshRatingValue(rating) })
}
</script>

<template>
  <div class="fresh-rating" role="group" :aria-label="t('feed.freshRating')" data-testid="fresh-rating">
    <button
      v-for="rating in freshRatings"
      :key="String(rating)"
      type="button"
      class="fresh-rating__option"
      :aria-pressed="rating === current"
      :data-rating="rating ?? 'all'"
      @click="rating === current || emit('choose', rating)"
    >
      {{ label(rating) }}
    </button>
  </div>
</template>

<style scoped>
.fresh-rating {
  display: flex;
  gap: var(--space-1);
  padding: 0 var(--space-3);
  overflow-x: auto;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  scrollbar-width: none;
}

.fresh-rating__option {
  display: inline-flex;
  flex: none;
  align-items: center;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border: 0;
  border-bottom: var(--size-focus-ring) solid transparent;
  background: none;
  color: var(--color-muted);
  font: inherit;
  font-weight: var(--font-weight-medium);
  white-space: nowrap;
  cursor: pointer;
}

.fresh-rating__option:hover {
  color: var(--color-text);
}

.fresh-rating__option[aria-pressed='true'] {
  border-bottom-color: var(--color-accent);
  color: var(--color-text);
}

.fresh-rating__option:focus-visible {
  outline-offset: calc(-1 * var(--size-focus-ring));
}
</style>
