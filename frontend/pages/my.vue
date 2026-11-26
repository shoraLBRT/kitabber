<script setup lang="ts">
// «Моя лента» (E08-04, FR-FEED, ADR-0089) at `/my`: the posts of the blogs and topics the person is subscribed to,
// newest first, page by page like the fresh feed (useFeed, ADR-0051). The feed needs the person's access token, so the
// first page is read on the site's server and later ones through it (server/routes/bff/my-feed.get.ts, ADR-0018). An
// empty feed offers topics to subscribe to; a guest is asked to sign in (the sign-in window, ADR-0032).
import FeedView from '~/components/feed/FeedView.vue'
import MyFeedSuggestions from '~/components/feed/MyFeedSuggestions.vue'
import UiButton from '~/components/ui/UiButton.vue'
import UiState from '~/components/ui/UiState.vue'
import { loadMyFeed, type FeedLookup } from '~/api/feeds'
import { listTopics } from '~/api/topics'
import { t } from '../i18n'

useHead({ title: t('nav.my') })

const account = useAccount()
const authWindow = useAuthWindow()
const { $api } = useNuxtApp()

function load(cursor?: string | null): Promise<FeedLookup> {
  if (import.meta.server) return loadMyFeed($api, cursor)
  return $fetch<FeedLookup>('/bff/my-feed', { query: cursor ? { cursor } : {} }).catch((): FeedLookup => ({
    status: 'error',
  }))
}

function signIn() {
  authWindow.value = { open: true, mode: 'sign-in' }
}

const feed = account.value.signedIn ? await useFeed('my', load) : null

// The topics an empty feed offers are read in the browser, only once the feed is known to be empty.
const empty = computed(() => feed?.state.value.status === 'found' && feed.state.value.posts.length === 0)
const { data: topics, execute: readTopics } = await useAsyncData('my-feed:topics', () => listTopics($api), {
  lazy: true,
  server: false,
  immediate: false,
})
watch(
  empty,
  (isEmpty) => {
    if (import.meta.client && isEmpty && !topics.value) void readTopics()
  },
  { immediate: true },
)
</script>

<template>
  <FeedView
    v-if="feed"
    :title="t('nav.my')"
    :state="feed.state.value"
    :loading-more="feed.loadingMore.value"
    :more-failed="feed.moreFailed.value"
    :empty-text="t('feed.myEmpty')"
    @retry="feed.retry()"
    @more="feed.loadMore()"
  >
    <template #empty-actions><MyFeedSuggestions :topics="topics ?? []" /></template>
  </FeedView>
  <section v-else class="my-feed-guest" aria-labelledby="feed-title" data-testid="my-feed-guest">
    <h1 id="feed-title" class="my-feed-guest__title">{{ t('nav.my') }}</h1>
    <UiState>
      {{ t('feed.myGuest') }}
      <template #actions>
        <UiButton @click="signIn()">{{ t('common.signIn') }}</UiButton>
      </template>
    </UiState>
  </section>
</template>

<style scoped>
.my-feed-guest {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.my-feed-guest__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}
</style>
