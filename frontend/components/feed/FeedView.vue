<script setup lang="ts">
// A feed (FR-FEED, docs/ui/feed.md): the posts in the feed's order, each as its card (FeedCard, E08-02, ADR-0052).
// «Показать новые» is E08-08 (#100).
// States: loading, empty, a failure with «Повторить», and the posts. The next page loads by itself when the end of the
// list comes near (E08-03, ADR-0051); a failure of a later page keeps the posts shown and offers «Повторить» instead
// of trying again on its own. Without IntersectionObserver «Показать ещё» asks for the next page.
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import FeedCard from './FeedCard.vue'
import FeedCardSkeleton from './FeedCardSkeleton.vue'
import UiState from '../ui/UiState.vue'
import type { FeedState } from '../../api/feeds'
import { t } from '../../i18n'

// A page with its own heading (the topic page, E11-01) gives no `title` but a `label` for the list's region, and may
// put its own words and action into the empty state.
const props = defineProps<{
  title?: string
  label?: string
  state: FeedState
  loadingMore?: boolean
  moreFailed?: boolean
  emptyText?: string
}>()

const emit = defineEmits<{ retry: []; more: [] }>()

// The next page is asked for while the end of the list is still this far below the screen.
const preloadMargin = '0px 0px 800px 0px'

const sentinel = ref<HTMLElement | null>(null)
const unobservable = ref(false)
let observer: IntersectionObserver | null = null

onMounted(() => {
  if (typeof IntersectionObserver === 'undefined') {
    unobservable.value = true
    return
  }
  observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) emit('more')
    },
    { rootMargin: preloadMargin },
  )
  if (sentinel.value) observer.observe(sentinel.value)
})

// The mark is drawn anew after every page, so a list still too short for the screen asks for the page after.
watch(sentinel, (element, previous) => {
  if (previous) observer?.unobserve(previous)
  if (element) observer?.observe(element)
})

onBeforeUnmount(() => observer?.disconnect())

const items = computed(() => (props.state.status === 'found' ? props.state.posts : []))
</script>

<template>
  <section
    class="feed"
    :aria-labelledby="title ? 'feed-title' : undefined"
    :aria-label="title ? undefined : label"
    data-testid="feed"
  >
    <h1 v-if="title" id="feed-title" class="feed__title">{{ title }}</h1>

    <div v-if="state.status === 'loading'" class="feed__list" aria-busy="true" data-testid="feed-loading">
      <span class="visually-hidden" role="status">{{ t('feed.loading') }}</span>
      <div v-for="n in 3" :key="n" class="feed__item"><FeedCardSkeleton /></div>
    </div>

    <UiState v-else-if="state.status === 'error'" tone="error" data-testid="feed-error">
      {{ t('feed.failed') }}
      <template #actions>
        <UiButton variant="secondary" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
      </template>
    </UiState>

    <UiState v-else-if="items.length === 0" data-testid="feed-empty">
      {{ emptyText ?? t('feed.empty') }}
      <template v-if="$slots['empty-actions']" #actions><slot name="empty-actions" /></template>
    </UiState>

    <template v-else>
      <ol class="feed__list" data-testid="feed-list">
        <li v-for="item in items" :key="item.post.id" class="feed__item" data-testid="feed-item">
          <FeedCard :item="item" />
        </li>
      </ol>
      <div v-if="state.status === 'found' && state.hasMore" class="feed__more">
        <template v-if="moreFailed">
          <p class="feed__muted" role="alert">{{ t('feed.moreFailed') }}</p>
          <UiButton
            variant="secondary"
            :disabled="loadingMore"
            data-testid="feed-more"
            @click="$emit('more')"
          >
            {{ loadingMore ? t('feed.loadingShort') : t('common.retry') }}
          </UiButton>
        </template>
        <div
          v-else-if="loadingMore"
          class="feed__item feed__next"
          role="status"
          data-testid="feed-loading-more"
        >
          <span class="visually-hidden">{{ t('feed.loadingMore') }}</span>
          <FeedCardSkeleton />
        </div>
        <div
          v-else-if="!unobservable"
          ref="sentinel"
          class="feed__sentinel"
          aria-hidden="true"
          data-testid="feed-sentinel"
        />
        <UiButton v-else variant="secondary" data-testid="feed-more" @click="$emit('more')">{{
          t('actions.showMore')
        }}</UiButton>
      </div>
    </template>
  </section>
</template>

<style scoped>
.feed {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.feed__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.feed__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  margin: 0;
  padding: 0;
  list-style: none;
}

.feed__item {
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

/* The skeleton of the next page: a card of the list's width under it. */
.feed__next {
  align-self: stretch;
}

.feed__muted {
  margin: 0;
  color: var(--color-muted);
}

.feed__more {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
}

/* The mark at the end of the list that asks for the next page; it takes almost no room. */
.feed__sentinel {
  width: 100%;
  height: var(--size-border);
}

@media (max-width: 639px) {
  .feed__item {
    border-radius: 0;
  }

  /* On a phone the cards take the whole width, closer to each other and with smaller fields (docs/ui/feed.md). */
  .feed__list {
    gap: var(--space-2);
  }

  .feed__item {
    padding: var(--space-4);
  }
}
</style>
