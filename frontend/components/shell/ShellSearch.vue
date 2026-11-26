<script setup lang="ts">
// The header's search (E14-02, FR-SEARCH, ADR-0097, docs/ui/README.md). On a desktop the field opens at the width of
// the feed over a darkened page; on a phone (below breakpoint.sm) it is a screen of its own with «Отмена». An empty
// field shows the query history of this browser and account, «Очистить» empties it; typing asks for suggestions after
// a pause (`suggestDelay`) and drops the answers of older queries. The field is a combobox: ↑ and ↓ pick a row, Enter
// opens it, Esc closes the search. A query goes into the history when a row is opened or Enter is pressed on it; the
// results page of Enter alone comes with E14-03.
import { History, Search, X } from 'lucide-vue-next'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  maxQueryLength,
  normalizeQuery,
  suggestDelay,
  suggestedBlogName,
  suggestedBlogPath,
  suggestedPostPath,
  type SearchApi,
  type SearchSuggestions,
} from '../../api/search'
import {
  clearSearchHistory,
  forgetSearch,
  historyStorage,
  readSearchHistory,
  rememberSearch,
  searchHistoryKey,
} from '../../utils/search-history'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiSkeleton from '../ui/UiSkeleton.vue'
import UiState from '../ui/UiState.vue'
import { t } from '../../i18n'

const props = withDefaults(defineProps<{ api: SearchApi; historyKey?: string | null }>(), {
  historyKey: null,
})
const emit = defineEmits<{ close: [] }>()

type Row =
  { kind: 'history'; query: string } | { kind: 'link'; path: string; group: 'blogs' | 'posts'; index: number }

const input = ref<HTMLInputElement | null>(null)
const panel = ref<HTMLElement | null>(null)
const text = ref('')
const query = computed(() => normalizeQuery(text.value))
const storage = historyStorage()
const storageKey = computed(() => searchHistoryKey(props.historyKey))
const history = ref<string[]>([])
const state = ref<'idle' | 'loading' | 'error' | 'done'>('idle')
const result = ref<SearchSuggestions | null>(null)
const active = ref(-1)
let timer: ReturnType<typeof setTimeout> | undefined
let controller: AbortController | undefined

const tooLong = computed(() => query.value.length > maxQueryLength)
const showsHistory = computed(() => query.value === '')
const rows = computed<Row[]>(() => {
  if (showsHistory.value) return history.value.map((q) => ({ kind: 'history', query: q }))
  if (state.value !== 'done' || !result.value) return []
  return [
    ...result.value.blogs.map((b, index) => ({
      kind: 'link' as const,
      path: suggestedBlogPath(b),
      group: 'blogs' as const,
      index,
    })),
    ...result.value.posts.map((p, index) => ({
      kind: 'link' as const,
      path: suggestedPostPath(p),
      group: 'posts' as const,
      index,
    })),
  ]
})
const empty = computed(
  () =>
    state.value === 'done' && !!result.value && result.value.blogs.length + result.value.posts.length === 0,
)

function optionId(i: number): string {
  return `search-option-${i}`
}

function rowIndex(group: 'blogs' | 'posts', index: number): number {
  return group === 'blogs' ? index : (result.value?.blogs.length ?? 0) + index
}

async function suggest(q: string) {
  controller?.abort()
  const current = new AbortController()
  controller = current
  state.value = 'loading'
  const answer = await props.api.suggest(q, current.signal)
  if (controller !== current) return
  if (answer.status === 200 && answer.body) {
    result.value = answer.body as SearchSuggestions
    state.value = 'done'
  } else {
    state.value = 'error'
  }
}

watch(query, (q) => {
  clearTimeout(timer)
  active.value = -1
  if (q === '' || q.length > maxQueryLength) {
    controller?.abort()
    controller = undefined
    result.value = null
    state.value = 'idle'
    return
  }
  timer = setTimeout(() => void suggest(q), suggestDelay)
})

function close() {
  emit('close')
}

function remember(q = query.value) {
  history.value = rememberSearch(storage, storageKey.value, q)
}

function pickHistory(q: string) {
  text.value = q
  input.value?.focus()
}

function forget(q: string) {
  history.value = forgetSearch(storage, storageKey.value, q)
  input.value?.focus()
}

function clearHistory() {
  history.value = clearSearchHistory(storage, storageKey.value)
  input.value?.focus()
}

// A row's link was followed (a click, or Enter on the active row): the query is kept and the search closes.
function opened() {
  remember()
  close()
}

function move(step: number) {
  const count = rows.value.length
  if (count === 0) return
  active.value = active.value < 0 ? (step > 0 ? 0 : count - 1) : (active.value + step + count) % count
  void nextTick(() => document.getElementById(optionId(active.value))?.scrollIntoView?.({ block: 'nearest' }))
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    move(event.key === 'ArrowDown' ? 1 : -1)
  } else if (event.key === 'Enter') {
    event.preventDefault()
    const row = rows.value[active.value]
    if (!row) {
      if (query.value && !tooLong.value) remember()
    } else if (row.kind === 'history') {
      pickHistory(row.query)
    } else {
      document.getElementById(optionId(active.value))?.querySelector('a')?.click()
    }
  } else if (event.key === 'Escape') {
    event.preventDefault()
    close()
  }
}

// The search is a modal dialog: Tab stays inside it.
function trapTab(event: KeyboardEvent) {
  if (event.key !== 'Tab' || !panel.value) return
  const focusable = [...panel.value.querySelectorAll<HTMLElement>('input, button:not([tabindex="-1"])')]
  const first = focusable[0]
  const last = focusable.at(-1)
  if (!first || !last) return
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

function retry() {
  if (query.value && !tooLong.value) void suggest(query.value)
}

onMounted(() => {
  history.value = readSearchHistory(storage, storageKey.value)
  input.value?.focus()
})

onBeforeUnmount(() => {
  clearTimeout(timer)
  controller?.abort()
})
</script>

<template>
  <div class="shell-search" data-testid="search">
    <div class="shell-search__backdrop" data-testid="search-backdrop" aria-hidden="true" @click="close()" />
    <div
      ref="panel"
      class="shell-search__panel"
      role="dialog"
      aria-modal="true"
      :aria-label="t('search.label')"
      @keydown="trapTab"
    >
      <div class="shell-search__bar">
        <Search class="shell-search__icon" aria-hidden="true" />
        <input
          ref="input"
          v-model="text"
          class="shell-search__input"
          type="search"
          role="combobox"
          enterkeyhint="search"
          autocomplete="off"
          :aria-label="t('search.label')"
          :placeholder="t('search.placeholder')"
          aria-autocomplete="list"
          aria-controls="search-listbox"
          :aria-expanded="rows.length > 0 ? 'true' : 'false'"
          :aria-activedescendant="active >= 0 ? optionId(active) : undefined"
          data-testid="search-input"
          @keydown="onKeydown"
        />
        <button type="button" class="shell-search__cancel" data-testid="search-cancel" @click="close()">
          {{ t('search.cancel') }}
        </button>
      </div>

      <div class="shell-search__body">
        <template v-if="showsHistory">
          <div v-if="history.length > 0" class="shell-search__section" data-testid="search-history">
            <div class="shell-search__heading">
              <h2 class="shell-search__title">{{ t('search.history') }}</h2>
              <button
                type="button"
                class="shell-search__link"
                data-testid="search-history-clear"
                @click="clearHistory()"
              >
                {{ t('search.clear') }}
              </button>
            </div>
            <ul
              id="search-listbox"
              class="shell-search__list"
              role="listbox"
              :aria-label="t('search.history')"
            >
              <li
                v-for="(q, i) in history"
                :id="optionId(i)"
                :key="q"
                role="option"
                :aria-selected="active === i ? 'true' : 'false'"
                :class="['shell-search__row', { 'shell-search__row--active': active === i }]"
                data-testid="search-history-row"
              >
                <button type="button" class="shell-search__row-main" tabindex="-1" @click="pickHistory(q)">
                  <History class="shell-search__row-icon" aria-hidden="true" />
                  <span class="shell-search__row-text">{{ q }}</span>
                </button>
                <button
                  type="button"
                  class="shell-search__forget"
                  :aria-label="t('search.forget', { query: q })"
                  @click="forget(q)"
                >
                  <X class="shell-search__row-icon" aria-hidden="true" />
                </button>
              </li>
            </ul>
          </div>
          <p v-else class="shell-search__hint" data-testid="search-history-empty">
            {{ t('search.historyEmpty') }}
          </p>
        </template>

        <UiState v-else-if="tooLong" tone="error" data-testid="search-too-long">
          {{ t('search.tooLong', { max: maxQueryLength }) }}
        </UiState>

        <div
          v-else-if="state === 'loading' || state === 'idle'"
          class="shell-search__loading"
          role="status"
          :aria-label="t('search.loading')"
          data-testid="search-loading"
        >
          <div v-for="n in 3" :key="n" class="shell-search__skeleton">
            <UiSkeleton shape="avatar" size="sm" />
            <UiSkeleton size="sm" width="70%" />
          </div>
        </div>

        <UiState v-else-if="state === 'error'" tone="error" data-testid="search-error">
          {{ t('search.failed') }}
          <template #actions>
            <UiButton variant="secondary" @click="retry()">{{ t('common.retry') }}</UiButton>
          </template>
        </UiState>

        <UiState v-else-if="empty" data-testid="search-empty">
          {{ t('search.nothingFound', { query }) }}
        </UiState>

        <div v-else-if="result" id="search-listbox" role="listbox" :aria-label="t('search.label')">
          <div
            v-if="result.blogs.length > 0"
            role="group"
            :aria-labelledby="'search-group-blogs'"
            class="shell-search__section"
          >
            <h2 id="search-group-blogs" class="shell-search__title">{{ t('search.blogs') }}</h2>
            <div
              v-for="(blog, i) in result.blogs"
              :id="optionId(rowIndex('blogs', i))"
              :key="blog.id"
              role="option"
              :aria-selected="active === rowIndex('blogs', i) ? 'true' : 'false'"
              :class="['shell-search__row', { 'shell-search__row--active': active === rowIndex('blogs', i) }]"
              data-testid="search-blog"
            >
              <NuxtLink
                :to="suggestedBlogPath(blog)"
                class="shell-search__row-main"
                tabindex="-1"
                @click="opened()"
              >
                <UiAvatar
                  :name="suggestedBlogName(blog) || '?'"
                  :sources="blog.avatarSources"
                  size="sm"
                  decorative
                />
                <span class="shell-search__row-text">{{ suggestedBlogName(blog) }}</span>
                <span class="shell-search__row-meta">{{
                  blog.kind === 'topic' ? t('search.topic') : t('search.blog')
                }}</span>
              </NuxtLink>
            </div>
          </div>
          <div
            v-if="result.posts.length > 0"
            role="group"
            :aria-labelledby="'search-group-posts'"
            class="shell-search__section"
          >
            <h2 id="search-group-posts" class="shell-search__title">{{ t('search.posts') }}</h2>
            <div
              v-for="(post, i) in result.posts"
              :id="optionId(rowIndex('posts', i))"
              :key="post.id"
              role="option"
              :aria-selected="active === rowIndex('posts', i) ? 'true' : 'false'"
              :class="['shell-search__row', { 'shell-search__row--active': active === rowIndex('posts', i) }]"
              data-testid="search-post"
            >
              <NuxtLink
                :to="suggestedPostPath(post)"
                class="shell-search__row-main shell-search__post"
                tabindex="-1"
                @click="opened()"
              >
                <span class="shell-search__row-text">{{ post.title ?? post.excerpt }}</span>
                <span v-if="post.title && post.excerpt" class="shell-search__row-excerpt">{{
                  post.excerpt
                }}</span>
              </NuxtLink>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.shell-search {
  position: fixed;
  inset: 0;
  z-index: 30;
}

.shell-search__backdrop {
  position: absolute;
  inset: 0;
  background: var(--color-overlay);
}

.shell-search__panel {
  position: absolute;
  top: calc(env(safe-area-inset-top) + var(--space-3));
  left: 50%;
  display: flex;
  flex-direction: column;
  width: min(var(--size-feed), calc(100vw - 2 * var(--space-4)));
  max-height: calc(100vh - 2 * var(--space-6));
  box-sizing: border-box;
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: 0 var(--space-2) var(--space-6) var(--color-shadow);
  transform: translateX(-50%);
}

.shell-search__bar {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  border-bottom: var(--size-border) solid var(--color-border);
}

.shell-search__icon {
  flex: none;
  width: var(--size-icon-md);
  height: var(--size-icon-md);
  color: var(--color-muted);
}

.shell-search__input {
  flex: 1;
  min-width: 0;
  min-height: var(--space-10);
  border: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
  font-size: var(--font-size-md);
}

.shell-search__input:focus {
  outline: none;
}

.shell-search__bar:focus-within {
  box-shadow: inset 0 calc(-1 * var(--size-focus-ring)) 0 var(--color-focus);
}

.shell-search__cancel {
  display: none;
  min-height: var(--size-touch);
  padding: 0 var(--space-2);
  border: 0;
  background: transparent;
  color: var(--color-accent);
  font: inherit;
  cursor: pointer;
}

.shell-search__body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: var(--space-2) 0;
}

.shell-search__section + .shell-search__section {
  margin-top: var(--space-2);
}

.shell-search__heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-right: var(--space-4);
}

.shell-search__title {
  margin: 0;
  padding: var(--space-2) var(--space-4);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.shell-search__link {
  border: 0;
  background: transparent;
  color: var(--color-accent);
  font: inherit;
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.shell-search__list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.shell-search__row {
  display: flex;
  align-items: center;
}

.shell-search__row--active,
.shell-search__row:hover {
  background: var(--color-surface-subtle);
}

.shell-search__row-main {
  display: flex;
  flex: 1;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
  min-height: var(--size-touch);
  box-sizing: border-box;
  padding: var(--space-2) var(--space-4);
  border: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
  text-align: left;
  text-decoration: none;
  cursor: pointer;
}

.shell-search__row-main:hover {
  color: var(--color-text);
}

.shell-search__post {
  flex-direction: column;
  align-items: flex-start;
  gap: 0;
}

.shell-search__row-text {
  overflow: hidden;
  min-width: 0;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.shell-search__post .shell-search__row-text {
  max-width: 100%;
}

.shell-search__row-meta {
  margin-left: auto;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.shell-search__row-excerpt {
  overflow: hidden;
  max-width: 100%;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.shell-search__row-icon {
  flex: none;
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  color: var(--color-muted);
}

.shell-search__forget {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--size-touch);
  height: var(--size-touch);
  border: 0;
  background: transparent;
  cursor: pointer;
}

.shell-search__hint {
  margin: 0;
  padding: var(--space-4);
  color: var(--color-muted);
}

.shell-search__loading {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-2) var(--space-4);
}

.shell-search__skeleton {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

/* Phone (below breakpoint.sm): a screen of its own with «Отмена» instead of a panel over the page. */
@media (max-width: 639px) {
  .shell-search__backdrop {
    display: none;
  }

  .shell-search__panel {
    inset: 0;
    width: auto;
    max-height: none;
    padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom)
      env(safe-area-inset-left);
    border: 0;
    border-radius: 0;
    box-shadow: none;
    transform: none;
  }

  .shell-search__cancel {
    display: inline-flex;
    align-items: center;
  }
}
</style>
