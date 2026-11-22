<script setup lang="ts">
// The fresh feed «Свежее» (E08-01, FR-FEED, docs/ui/feed.md) at `/new`: every published post, newest first. The first
// page is rendered on the server; later pages load as the reader scrolls, and «назад» from a post comes back to the
// same place (E08-03, useFeed, ADR-0051). The feed is public, so the browser asks the API directly.
import FeedView from '~/components/feed/FeedView.vue'
import { loadFreshFeed } from '~/api/feeds'

const { $api } = useNuxtApp()

const { state, loadingMore, moreFailed, loadMore, retry } = await useFeed('fresh', (cursor) =>
  loadFreshFeed($api, cursor),
)

useHead({ title: 'Свежее' })
</script>

<template>
  <FeedView
    title="Свежее"
    :state="state"
    :loading-more="loadingMore"
    :more-failed="moreFailed"
    @retry="retry()"
    @more="loadMore()"
  />
</template>
