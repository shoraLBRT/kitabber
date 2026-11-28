<script setup lang="ts">
// The filters of the block «Посты» of the search results (E14-03, FR-SEARCH, ADR-0124): «Только заголовки», the period
// and the order. Each change is reported at once; the page keeps the filters in the address.
import { useId } from 'vue'
import {
  searchOrders,
  searchPeriods,
  type SearchFilters,
  type SearchOrder,
  type SearchPeriod,
} from '../../api/search'
import { t } from '../../i18n'

const props = defineProps<{ filters: SearchFilters }>()
const emit = defineEmits<{ change: [filters: SearchFilters] }>()

const id = useId()

function set(change: Partial<SearchFilters>) {
  emit('change', { ...props.filters, ...change })
}

function choosePeriod(period: SearchPeriod) {
  if (period !== props.filters.period) set({ period })
}

function chooseOrder(order: SearchOrder) {
  if (order !== props.filters.order) set({ order })
}
</script>

<template>
  <div class="search-filters" data-testid="search-filters">
    <label class="search-filters__check">
      <input
        type="checkbox"
        :checked="filters.titlesOnly"
        data-testid="search-filter-titles"
        @change="set({ titlesOnly: ($event.target as HTMLInputElement).checked })"
      />
      {{ t('searchResults.titlesOnly') }}
    </label>

    <div class="search-filters__group">
      <span :id="`${id}-period`" class="search-filters__label">{{ t('searchResults.period') }}</span>
      <div class="search-filters__options" role="group" :aria-labelledby="`${id}-period`">
        <button
          v-for="period in searchPeriods"
          :key="period"
          type="button"
          class="search-filters__option"
          :aria-pressed="period === filters.period"
          :data-period="period"
          @click="choosePeriod(period)"
        >
          {{ t(`searchResults.periods.${period}`) }}
        </button>
      </div>
    </div>

    <div class="search-filters__group">
      <span :id="`${id}-order`" class="search-filters__label">{{ t('searchResults.order') }}</span>
      <div class="search-filters__options" role="group" :aria-labelledby="`${id}-order`">
        <button
          v-for="order in searchOrders"
          :key="order"
          type="button"
          class="search-filters__option"
          :aria-pressed="order === filters.order"
          :data-order="order"
          @click="chooseOrder(order)"
        >
          {{ t(`searchResults.orders.${order}`) }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.search-filters {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.search-filters__check {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: var(--size-touch);
  cursor: pointer;
}

.search-filters__group {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.search-filters__label {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.search-filters__options {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-1);
}

.search-filters__option {
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font: inherit;
  cursor: pointer;
}

.search-filters__option[aria-pressed='true'] {
  border-color: var(--color-accent);
  color: var(--color-accent);
  font-weight: var(--font-weight-medium);
}
</style>
