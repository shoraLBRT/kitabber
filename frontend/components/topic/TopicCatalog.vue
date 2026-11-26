<script setup lang="ts">
// The catalogue of topics behind «Показать все» (E11-02, FR-TOPIC, ADR-0088): a search by name or address and the
// topics, the most followed first, each with its description and number of subscribers, leading to its page
// /<topic>. «Показать ещё» reads the next page. The states of docs/ui/README.md: skeletons while loading, a quiet text
// when there is nothing (no topics at all, or none for the search), an error with «Повторить».
import { computed } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiSkeleton from '../ui/UiSkeleton.vue'
import UiState from '../ui/UiState.vue'
import { compactCount } from '../../api/subscriptions'
import { catalogQueryMaxLength, type TopicCard } from '../../api/topics'
import { t } from '../../i18n'

export type CatalogState =
  { status: 'loading' } | { status: 'error' } | { status: 'found'; topics: TopicCard[]; hasMore: boolean }

const props = withDefaults(
  defineProps<{
    state: CatalogState
    /** The text the shown topics were searched by; empty for every topic. */
    searched?: string
    loadingMore?: boolean
    moreFailed?: boolean
  }>(),
  { searched: '', loadingMore: false, moreFailed: false },
)
const search = defineModel<string>('search', { default: '' })
const emit = defineEmits<{ retry: []; more: [] }>()

const emptyText = computed(() =>
  props.searched.trim()
    ? t('topicCatalog.notFound', { query: props.searched.trim() })
    : t('topicCatalog.empty'),
)

const subscribers = (count: number) => t('subscription.subscribers', { count, shown: compactCount(count) })
</script>

<template>
  <section class="topic-catalog" aria-labelledby="topic-catalog-title" data-testid="topic-catalog">
    <h1 id="topic-catalog-title" class="topic-catalog__title">{{ t('topicCatalog.title') }}</h1>
    <input
      v-model="search"
      type="search"
      class="topic-catalog__search"
      :aria-label="t('topicCatalog.search')"
      :placeholder="t('topicCatalog.searchPlaceholder')"
      :maxlength="catalogQueryMaxLength"
      enterkeyhint="search"
      data-testid="topic-search"
    />

    <div
      v-if="state.status === 'loading'"
      class="topic-catalog__card"
      aria-busy="true"
      data-testid="topic-catalog-loading"
    >
      <span class="visually-hidden" role="status">{{ t('topicCatalog.loading') }}</span>
      <div v-for="row in 4" :key="row" class="topic-catalog__row">
        <UiSkeleton shape="avatar" size="md" />
        <span class="topic-catalog__text">
          <UiSkeleton width="40%" />
          <UiSkeleton size="sm" width="70%" />
        </span>
      </div>
    </div>

    <UiState v-else-if="state.status === 'error'" tone="error">
      {{ t('topicCatalog.failed') }}
      <template #actions>
        <UiButton variant="secondary" size="sm" @click="emit('retry')">{{ t('common.retry') }}</UiButton>
      </template>
    </UiState>

    <UiState v-else-if="state.topics.length === 0">{{ emptyText }}</UiState>

    <template v-else>
      <ul class="topic-catalog__card topic-catalog__list" :aria-label="t('topicCatalog.list')">
        <li v-for="topic in state.topics" :key="topic.id">
          <NuxtLink :to="`/${topic.nickname}`" class="topic-catalog__row" data-testid="topic-card">
            <UiAvatar :name="topic.name" size="md" decorative />
            <span class="topic-catalog__text">
              <span class="topic-catalog__name">{{ topic.name }}</span>
              <span v-if="topic.description" class="topic-catalog__description">{{ topic.description }}</span>
              <span class="topic-catalog__count" data-testid="topic-subscribers">{{
                subscribers(topic.subscriberCount)
              }}</span>
            </span>
          </NuxtLink>
        </li>
      </ul>
      <p v-if="moreFailed" class="topic-catalog__more-failed" role="alert">
        {{ t('topicCatalog.moreFailed') }}
      </p>
      <UiButton
        v-if="state.hasMore"
        variant="secondary"
        :loading="loadingMore"
        data-testid="topic-catalog-more"
        @click="emit('more')"
        >{{ moreFailed ? t('common.retry') : t('topicCatalog.more') }}</UiButton
      >
    </template>
  </section>
</template>

<style scoped>
.topic-catalog {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.topic-catalog__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

.topic-catalog__search {
  width: 100%;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font: inherit;
}

.topic-catalog__card {
  display: flex;
  flex-direction: column;
  padding: var(--space-2);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.topic-catalog__list {
  margin: 0;
  list-style: none;
}

.topic-catalog__row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-height: var(--size-touch);
  padding: var(--space-3);
  border-radius: var(--radius-md);
  color: var(--color-text);
  text-decoration: none;
}

a.topic-catalog__row:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.topic-catalog__text {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.topic-catalog__name {
  font-weight: var(--font-weight-medium);
  overflow-wrap: anywhere;
}

.topic-catalog__description {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  overflow-wrap: anywhere;
}

.topic-catalog__count {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.topic-catalog__more-failed {
  margin: 0;
  color: var(--color-text);
  font-size: var(--font-size-sm);
}
</style>
