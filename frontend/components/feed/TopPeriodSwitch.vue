<script setup lang="ts">
// The switch of the period in «Популярном» (E08-06, FR-FEED, ADR-0104): a link to the top of each period, the shown
// one marked as the current page. Each period has an address of its own (`/popular/week`), so a period is shared and
// comes back with «назад» like any page. On a phone the row scrolls sideways instead of wrapping.
import { topPeriodPath, topPeriods, type TopPeriod } from '../../api/feeds'
import { t } from '../../i18n'

defineProps<{ current: TopPeriod }>()

const labels = {
  day: t('feed.topDay'),
  week: t('feed.topWeek'),
  month: t('feed.topMonth'),
  year: t('feed.topYear'),
  all: t('feed.topAll'),
} satisfies Record<TopPeriod, string>
</script>

<template>
  <nav class="top-periods" :aria-label="t('feed.topPeriods')" data-testid="top-periods">
    <NuxtLink
      v-for="period in topPeriods"
      :key="period"
      :to="topPeriodPath(period)"
      class="top-periods__link"
      :aria-current="period === current ? 'page' : undefined"
      :data-period="period"
    >
      {{ labels[period] }}
    </NuxtLink>
  </nav>
</template>

<style scoped>
.top-periods {
  display: flex;
  gap: var(--space-1);
  padding: 0 var(--space-3);
  overflow-x: auto;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  scrollbar-width: none;
}

.top-periods__link {
  display: inline-flex;
  flex: none;
  align-items: center;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border-bottom: var(--size-focus-ring) solid transparent;
  color: var(--color-muted);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
  white-space: nowrap;
}

.top-periods__link:hover {
  color: var(--color-text);
}

.top-periods__link[aria-current='page'] {
  border-bottom-color: var(--color-accent);
  color: var(--color-text);
}

.top-periods__link:focus-visible {
  outline-offset: calc(-1 * var(--size-focus-ring));
}
</style>
