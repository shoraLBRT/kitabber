<script setup lang="ts">
// The posts with a hashtag (E11-08, FR-TOPIC, ADR-0120): GET /api/v1/feeds/tags/{tag}, newest first, the first page on
// the server and the next ones as the reader scrolls (useFeed, ADR-0051). The feed is public, so the browser asks the
// API directly. On a phone the feed is read again by pulling it down (E08-14, ADR-0111).
import FeedView from '../feed/FeedView.vue'
import PullToRefresh from '../feed/PullToRefresh.vue'
import { loadTagFeed } from '../../api/feeds'
import { t } from '../../i18n'

const props = defineProps<{ tag: string }>()

const { $api } = useNuxtApp()

const { state, loadingMore, moreFailed, loadMore, retry } = await useFeed(`tag:${props.tag}`, (cursor) =>
  loadTagFeed($api, props.tag, cursor),
)
</script>

<template>
  <PullToRefresh :refresh="retry">
    <FeedView
      :title="`#${tag}`"
      :state="state"
      :loading-more="loadingMore"
      :more-failed="moreFailed"
      :empty-text="t('hashtag.noPosts')"
      @retry="retry()"
      @more="loadMore()"
    />
  </PullToRefresh>
</template>
