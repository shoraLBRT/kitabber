<script setup lang="ts">
// The search results (E14-03, FR-SEARCH, docs/ui/README.md, ADR-0124): the blocks «Блоги», «Темы» and «Посты» of a
// query. Blog and topic rows are those of the subscription lists, with «Подписаться» (none on the reader's own blog);
// each block has «Показать ещё» while there is more. «Посты» has the button «Фильтры», which opens «Только заголовки»,
// the period and the order. The states of docs/ui/README.md: skeletons while searching, an error with «Повторить», and a
// placeholder when nothing at all is found. A block that found nothing is left out; «Посты» stays while a filter is on,
// so it can be changed back. The page holds the state (pages/rating/index.vue); this only shows it and reports presses.
import { computed, ref, useId } from 'vue'
import SubscriptionList from '../blog/SubscriptionList.vue'
import SearchPostFilters from './SearchPostFilters.vue'
import UiButton from '../ui/UiButton.vue'
import UiSkeleton from '../ui/UiSkeleton.vue'
import UiState from '../ui/UiState.vue'
import {
  filtered,
  suggestedBlogName,
  suggestedPostPath,
  type FoundBlog,
  type FoundPost,
  type SearchFilters,
} from '../../api/search'
import type { SubscriptionCard } from '../../api/subscriptions'
import { locale, t } from '../../i18n'

/** A block's rows as read so far. */
export interface FoundBlock<T> {
  status: 'loading' | 'error' | 'found'
  items: T[]
  hasMore: boolean
  loadingMore: boolean
  moreFailed: boolean
}

export type SearchBlock = 'blogs' | 'topics' | 'posts'

const props = withDefaults(
  defineProps<{
    query: string
    blogs: FoundBlock<FoundBlog>
    topics: FoundBlock<FoundBlog>
    posts: FoundBlock<FoundPost>
    filters: SearchFilters
    /** The rows whose subscription is being changed: their buttons wait. */
    busy?: readonly string[]
  }>(),
  { busy: () => [] },
)
const emit = defineEmits<{
  retry: []
  more: [block: SearchBlock]
  toggle: [row: SubscriptionCard]
  filters: [filters: SearchFilters]
}>()

const id = useId()
const filtersOpen = ref(filtered(props.filters))

const blocks = computed(() => [props.blogs, props.topics, props.posts])
const loading = computed(() => blocks.value.some((b) => b.status === 'loading') && !blocks.value.some(found))
const failed = computed(() => blocks.value.every((b) => b.status === 'error'))
const nothing = computed(
  () => blocks.value.every((b) => b.status === 'found' && b.items.length === 0) && !filtered(props.filters),
)
const postsShown = computed(
  () => props.posts.status !== 'found' || props.posts.items.length > 0 || filtered(props.filters),
)

function found(block: FoundBlock<unknown>) {
  return block.status === 'found' && block.items.length > 0
}

const dateFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'UTC',
})
const date = (at: string) => dateFormat.format(new Date(at))
</script>

<template>
  <div class="search-results" data-testid="search-results">
    <div v-if="loading" class="search-results__card" aria-busy="true" data-testid="search-results-loading">
      <span class="visually-hidden" role="status">{{ t('searchResults.loading') }}</span>
      <div v-for="row in 4" :key="row" class="search-results__skeleton">
        <UiSkeleton shape="avatar" size="md" />
        <span class="search-results__skeleton-text">
          <UiSkeleton width="40%" />
          <UiSkeleton size="sm" width="70%" />
        </span>
      </div>
    </div>

    <UiState v-else-if="failed" tone="error">
      {{ t('searchResults.failed') }}
      <template #actions>
        <UiButton variant="secondary" size="sm" data-testid="search-results-retry" @click="emit('retry')">{{
          t('common.retry')
        }}</UiButton>
      </template>
    </UiState>

    <UiState v-else-if="nothing">
      <span data-testid="search-results-empty">{{ t('searchResults.nothingFound', { query }) }}</span>
      <template #actions>
        <span class="search-results__hint">{{ t('searchResults.nothingFoundHint') }}</span>
      </template>
    </UiState>

    <template v-else>
      <template v-for="block in ['blogs', 'topics'] as const" :key="block">
        <section
          v-if="found(props[block])"
          class="search-results__card"
          :aria-labelledby="`${id}-${block}`"
          :data-testid="`search-block-${block}`"
        >
          <h2 :id="`${id}-${block}`" class="search-results__title">{{ t(`searchResults.${block}`) }}</h2>
          <SubscriptionList
            kind="subscriptions"
            :label="t(`searchResults.${block}`)"
            :state="{ status: 'found', cards: props[block].items, hasMore: props[block].hasMore }"
            :loading-more="props[block].loadingMore"
            :more-failed="props[block].moreFailed"
            :busy="busy"
            @more="emit('more', block)"
            @toggle="emit('toggle', $event)"
          />
        </section>
      </template>

      <section
        v-if="postsShown"
        class="search-results__card"
        :aria-labelledby="`${id}-posts`"
        data-testid="search-block-posts"
      >
        <div class="search-results__head">
          <h2 :id="`${id}-posts`" class="search-results__title">{{ t('searchResults.posts') }}</h2>
          <UiButton
            variant="secondary"
            size="sm"
            :aria-expanded="filtersOpen ? 'true' : 'false'"
            :aria-controls="`${id}-filters`"
            data-testid="search-filters-toggle"
            @click="filtersOpen = !filtersOpen"
            >{{ t('searchResults.filters') }}</UiButton
          >
        </div>
        <div v-show="filtersOpen" :id="`${id}-filters`">
          <SearchPostFilters :filters="filters" @change="emit('filters', $event)" />
        </div>

        <div v-if="posts.status === 'loading'" aria-busy="true" data-testid="search-posts-loading">
          <span class="visually-hidden" role="status">{{ t('searchResults.loading') }}</span>
          <div v-for="row in 3" :key="row" class="search-results__post">
            <UiSkeleton width="60%" />
            <UiSkeleton size="sm" width="90%" />
          </div>
        </div>

        <UiState v-else-if="posts.status === 'error'" tone="error">
          {{ t('searchResults.failed') }}
          <template #actions>
            <UiButton variant="secondary" size="sm" @click="emit('retry')">{{ t('common.retry') }}</UiButton>
          </template>
        </UiState>

        <p v-else-if="posts.items.length === 0" class="search-results__none" data-testid="search-posts-empty">
          {{ t('searchResults.noPosts') }}
        </p>

        <template v-else>
          <ul class="search-results__posts" :aria-label="t('searchResults.posts')">
            <li
              v-for="post in posts.items"
              :key="post.id"
              class="search-results__post"
              data-testid="search-result-post"
            >
              <NuxtLink :to="suggestedPostPath(post)" class="search-results__post-title">{{
                post.title ?? t('searchResults.untitledPost')
              }}</NuxtLink>
              <span v-if="post.excerpt" class="search-results__excerpt">{{ post.excerpt }}</span>
              <span class="search-results__meta">
                <template v-if="post.blog">{{ suggestedBlogName(post.blog) }} · </template>
                <time :datetime="post.publishedAt">{{ date(post.publishedAt) }}</time>
              </span>
            </li>
          </ul>
          <p v-if="posts.moreFailed" class="search-results__none" role="alert">
            {{ t('searchResults.moreFailed') }}
          </p>
          <UiButton
            v-if="posts.hasMore"
            variant="secondary"
            :loading="posts.loadingMore"
            data-testid="search-posts-more"
            @click="emit('more', 'posts')"
            >{{ posts.moreFailed ? t('common.retry') : t('searchResults.more') }}</UiButton
          >
        </template>
      </section>
    </template>
  </div>
</template>

<style scoped>
.search-results {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.search-results__card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.search-results__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.search-results__title {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
}

.search-results__skeleton {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2) 0;
}

.search-results__skeleton-text {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-1);
}

.search-results__posts {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.search-results__post {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
  padding: var(--space-2) 0;
}

.search-results__post-title {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  overflow-wrap: anywhere;
  text-decoration: none;
}

.search-results__post-title:hover {
  color: var(--color-accent);
}

.search-results__excerpt {
  overflow-wrap: anywhere;
}

.search-results__meta,
.search-results__hint {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.search-results__none {
  margin: 0;
  color: var(--color-muted);
}
</style>
