<script setup lang="ts">
// The fresh feed «Свежее» (E08-01, FR-FEED, docs/ui/feed.md) at `/new`: every published post, newest first. The first
// page is rendered on the server; later pages come from the browser with the cursor of the page before (ADR-0043).
// The feed is public, so the browser asks the API directly.
import FeedView from '~/components/feed/FeedView.vue'
import { appendPage, loadFreshFeed, type FeedLookup, type FeedState } from '~/api/feeds'
import type { Post } from '~/api/posts'

const { $api } = useNuxtApp()

const { data, pending, refresh } = await useAsyncData(
  'feed:fresh',
  (): Promise<FeedLookup> => loadFreshFeed($api),
  {
    lazy: true,
  },
)

// The pages read after the first one; they start again whenever the first page is read again.
const more = ref<Post[]>([])
const cursor = ref<string | null>(null)
const loadingMore = ref(false)
const moreFailed = ref(false)

watch(
  data,
  (first) => {
    more.value = []
    moreFailed.value = false
    cursor.value = first?.status === 'found' ? first.page.nextCursor : null
  },
  { immediate: true },
)

const state = computed<FeedState>(() => {
  if (pending.value || !data.value) return { status: 'loading' }
  if (data.value.status === 'error') return { status: 'error' }
  const shown = appendPage(data.value.page.items, { items: more.value, nextCursor: cursor.value })
  return { status: 'found', posts: shown, hasMore: cursor.value !== null }
})

async function loadMore() {
  if (!cursor.value || loadingMore.value) return
  loadingMore.value = true
  const next = await loadFreshFeed($api, cursor.value)
  loadingMore.value = false
  if (next.status === 'error') {
    moreFailed.value = true
    return
  }
  moreFailed.value = false
  more.value = [...more.value, ...next.page.items]
  cursor.value = next.page.nextCursor
}

useHead({ title: 'Свежее' })
</script>

<template>
  <FeedView
    title="Свежее"
    :state="state"
    :loading-more="loadingMore"
    :more-failed="moreFailed"
    @retry="refresh()"
    @more="loadMore()"
  />
</template>
