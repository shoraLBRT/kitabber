<script setup lang="ts">
// The fresh feed «Свежее» (E08-01, FR-FEED, docs/ui/feed.md) at `/new`: every published post, newest first. The first
// page is rendered on the server; later pages load as the reader scrolls, and «назад» from a post comes back to the
// same place (E08-03, useFeed, ADR-0051). The feed is public, so the browser asks the API directly. Above it on a
// desktop is the quick post (E05-29, ADR-0055); on a phone the floating «Написать» opens it.
import FeedView from '~/components/feed/FeedView.vue'
import QuickPost from '~/components/feed/QuickPost.vue'
import { loadFreshFeed } from '~/api/feeds'

const { $api } = useNuxtApp()

const { state, loadingMore, moreFailed, loadMore, retry } = await useFeed('fresh', (cursor) =>
  loadFreshFeed($api, cursor),
)

useHead({ title: 'Свежее' })
</script>

<template>
  <div class="feed-page">
    <QuickPost class="feed-page__quick-post" />
    <FeedView
      title="Свежее"
      :state="state"
      :loading-more="loadingMore"
      :more-failed="moreFailed"
      @retry="retry()"
      @more="loadMore()"
    />
  </div>
</template>

<style scoped>
.feed-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

@media (max-width: 639px) {
  .feed-page__quick-post {
    display: none;
  }
}
</style>
