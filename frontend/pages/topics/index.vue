<script setup lang="ts">
// The catalogue of topics at /topics (E11-02, FR-TOPIC, ADR-0088), where «Показать все» of the menu leads: every topic,
// the most followed first, searched by name or address. The catalogue is public, so the API is asked directly (the
// first page on the server); the search waits for a pause in typing and stays in the address (?q=), so a search can be
// shared and survives a reload. Later pages come with «Показать ещё».
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import TopicCatalog, { type CatalogState } from '~/components/topic/TopicCatalog.vue'
import { appendTopics, loadTopicCatalog, type TopicCard } from '~/api/topics'
import { t } from '../../i18n'

/** How long the search waits after the last key, in milliseconds. */
const searchPause = 300

useHead({ title: t('topicCatalog.title') })

const route = useRoute()
const router = useRouter()
const { $api } = useNuxtApp()

const initial = typeof route.query.q === 'string' ? route.query.q.trim() : ''
const search = ref(initial)
const searched = ref(initial)

let timer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(timer)
  timer = setTimeout(() => {
    searched.value = value.trim()
  }, searchPause)
})
onBeforeUnmount(() => clearTimeout(timer))
watch(searched, (value) => {
  void router.replace({ query: value ? { q: value } : {} })
})

const { data, status, refresh } = await useAsyncData(
  'topics-catalog',
  () => loadTopicCatalog($api, searched.value),
  { watch: [searched] },
)

// The pages after the first, read with «Показать ещё».
const more = ref<TopicCard[]>([])
const nextCursor = ref<string | null>(null)
const loadingMore = ref(false)
const moreFailed = ref(false)

watch(
  data,
  (lookup) => {
    more.value = []
    moreFailed.value = false
    nextCursor.value = lookup?.status === 'found' ? (lookup.page.nextCursor ?? null) : null
  },
  { immediate: true },
)

const state = computed<CatalogState>(() => {
  if (status.value === 'pending' || (status.value === 'idle' && !data.value)) return { status: 'loading' }
  const lookup = data.value
  if (!lookup || lookup.status !== 'found') return { status: 'error' }
  return {
    status: 'found',
    topics: appendTopics(lookup.page.items, { items: more.value, nextCursor: null }),
    hasMore: nextCursor.value !== null,
  }
})

async function loadMore() {
  const cursor = nextCursor.value
  if (!cursor || loadingMore.value) return
  const asked = searched.value
  loadingMore.value = true
  const lookup = await loadTopicCatalog($api, asked, cursor)
  loadingMore.value = false
  // A new search started meanwhile: its first page replaces these.
  if (asked !== searched.value || cursor !== nextCursor.value) return
  if (lookup.status !== 'found') {
    moreFailed.value = true
    return
  }
  moreFailed.value = false
  more.value = appendTopics(more.value, lookup.page)
  nextCursor.value = lookup.page.nextCursor ?? null
}
</script>

<template>
  <TopicCatalog
    v-model:search="search"
    :state="state"
    :searched="searched"
    :loading-more="loadingMore"
    :more-failed="moreFailed"
    @retry="refresh()"
    @more="loadMore()"
  />
</template>
