<script setup lang="ts">
// The section «Рейтинг» at /rating (FR-SEARCH, docs/ui/README.md). For now it holds the search results (E14-03,
// ADR-0124): the query in the address (?q=, where Enter in the header's search leads) and the filters of «Посты»
// (?titles=1, ?period=, ?order=), so a search can be shared and survives a reload. The field with «Отмена» searches
// again on Enter; «Отмена» leaves the search. The blocks «Блоги», «Темы» and «Посты» are read at once through the
// site's own server (they depend on the reader's subscriptions and black list), each block's «Показать ещё» reads that
// block's next page, and a change of the filters reads «Посты» again. The top of blogs, the topics and the recommended
// posts of the section come with E14-04…E14-06.
import { computed, onMounted, ref, watch, type Ref } from 'vue'
import SearchResults, { type FoundBlock, type SearchBlock } from '~/components/search/SearchResults.vue'
import UiState from '~/components/ui/UiState.vue'
import {
  appendFound,
  filtersOf,
  maxQueryLength,
  normalizeQuery,
  resultsQuery,
  searchResultsApi,
  type FoundBlog,
  type FoundPost,
  type SearchBlogKind,
  type SearchFilters,
} from '~/api/search'
import { t } from '~/i18n'

const route = useRoute()
const router = useRouter()
const api = searchResultsApi()

const query = computed(() => (typeof route.query.q === 'string' ? normalizeQuery(route.query.q) : ''))
const filters = computed(() => filtersOf(route.query))
const field = ref(query.value)
watch(query, (value) => {
  field.value = value
})

useHead(() => ({
  title: query.value ? `${t('searchResults.title')}: ${query.value}` : t('searchResults.title'),
}))

function block<T>(): FoundBlock<T> {
  return { status: 'loading', items: [], hasMore: false, loadingMore: false, moreFailed: false }
}

const blogs = ref(block<FoundBlog>()) as Ref<FoundBlock<FoundBlog>>
const topics = ref(block<FoundBlog>()) as Ref<FoundBlock<FoundBlog>>
const posts = ref(block<FoundPost>()) as Ref<FoundBlock<FoundPost>>
const blogBlocks: Record<'blogs' | 'topics', { kind: SearchBlogKind; state: Ref<FoundBlock<FoundBlog>> }> = {
  blogs: { kind: 'personal', state: blogs },
  topics: { kind: 'topic', state: topics },
}

// What a block was last asked for: an answer to an older question is dropped.
const asked: Record<SearchBlock, number> = { blogs: 0, topics: 0, posts: 0 }

async function loadBlogs(name: 'blogs' | 'topics') {
  const { kind, state } = blogBlocks[name]
  const mine = ++asked[name]
  state.value = block<FoundBlog>()
  const lookup = await api.blogs(query.value, kind, 0)
  if (mine !== asked[name]) return
  state.value =
    lookup.status === 'found'
      ? { ...block<FoundBlog>(), status: 'found', items: lookup.page.items, hasMore: lookup.page.hasMore }
      : { ...block<FoundBlog>(), status: 'error' }
}

async function loadPosts() {
  const mine = ++asked.posts
  posts.value = block<FoundPost>()
  const lookup = await api.posts(query.value, filters.value, 0)
  if (mine !== asked.posts) return
  posts.value =
    lookup.status === 'found'
      ? { ...block<FoundPost>(), status: 'found', items: lookup.page.items, hasMore: lookup.page.hasMore }
      : { ...block<FoundPost>(), status: 'error' }
}

function loadAll() {
  if (!query.value) return
  void loadBlogs('blogs')
  void loadBlogs('topics')
  void loadPosts()
}

async function more(name: SearchBlock) {
  const state: Ref<FoundBlock<FoundBlog | FoundPost>> = name === 'posts' ? posts : blogBlocks[name].state
  if (state.value.loadingMore || !state.value.hasMore) return
  const mine = asked[name]
  state.value = { ...state.value, loadingMore: true }
  const offset = state.value.items.length
  const lookup =
    name === 'posts'
      ? await api.posts(query.value, filters.value, offset)
      : await api.blogs(query.value, blogBlocks[name].kind, offset)
  if (mine !== asked[name]) return
  state.value =
    lookup.status === 'found'
      ? {
          ...state.value,
          items: appendFound<FoundBlog | FoundPost>(state.value.items, lookup.page.items),
          hasMore: lookup.page.hasMore,
          loadingMore: false,
          moreFailed: false,
        }
      : { ...state.value, loadingMore: false, moreFailed: true }
}

// The results are the reader's own, so they are read in the browser, once it is there.
onMounted(loadAll)
watch(query, loadAll)
watch(
  () => JSON.stringify(filters.value),
  () => {
    if (query.value) void loadPosts()
  },
)

function search() {
  const value = normalizeQuery(field.value)
  if (!value || value.length > maxQueryLength) return
  if (value === query.value) loadAll()
  else void router.push({ path: '/rating', query: resultsQuery(value, filters.value) })
}

function cancel() {
  field.value = ''
  void router.push({ path: '/rating' })
}

function changeFilters(next: SearchFilters) {
  void router.replace({ path: '/rating', query: resultsQuery(query.value, next) })
}

function mark(id: string, subscribed: boolean) {
  for (const state of [blogs, topics])
    state.value = {
      ...state.value,
      items: state.value.items.map((row) => (row.id === id ? { ...row, subscribed } : row)),
    }
}

const { busy, toggle } = useRowSubscriptions(mark, (id) => `subscribe-search:${id}`)
</script>

<template>
  <section class="rating-search" aria-labelledby="rating-search-title" data-testid="rating-search">
    <h1 id="rating-search-title" class="visually-hidden">{{ t('searchResults.title') }}</h1>
    <form class="rating-search__form" role="search" @submit.prevent="search()">
      <input
        v-model="field"
        type="search"
        class="rating-search__field"
        :aria-label="t('searchResults.field')"
        :placeholder="t('searchResults.placeholder')"
        :maxlength="maxQueryLength"
        enterkeyhint="search"
        data-testid="rating-search-field"
      />
      <button
        v-if="query || field"
        type="button"
        class="rating-search__cancel"
        data-testid="rating-search-cancel"
        @click="cancel()"
      >
        {{ t('searchResults.cancel') }}
      </button>
    </form>

    <SearchResults
      v-if="query"
      :key="query"
      :query="query"
      :blogs="blogs"
      :topics="topics"
      :posts="posts"
      :filters="filters"
      :busy="busy"
      @retry="loadAll()"
      @more="more"
      @toggle="toggle"
      @filters="changeFilters"
    />
    <UiState v-else>{{ t('searchResults.prompt') }}</UiState>
  </section>
</template>

<style scoped>
.rating-search {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.rating-search__form {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.rating-search__field {
  flex: 1;
  min-width: 0;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font: inherit;
}

.rating-search__cancel {
  flex: none;
  min-height: var(--size-touch);
  padding: 0 var(--space-2);
  border: 0;
  background: none;
  color: var(--color-accent);
  font: inherit;
  cursor: pointer;
}
</style>
