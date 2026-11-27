<script setup lang="ts">
// The posts of a topic (E11-01, ADR-0082): GET /api/v1/feeds/blogs/{id}, newest first, the first page on the server
// and the next ones as the reader scrolls (useFeed, ADR-0051). The feed is public, so the browser asks the API
// directly. An empty topic offers «Написать в тему»: the editor opens with this topic chosen. On a phone the feed is
// read again by pulling it down (E08-14, ADR-0111).
import FeedView from '../feed/FeedView.vue'
import PullToRefresh from '../feed/PullToRefresh.vue'
import { loadBlogFeed } from '../../api/feeds'
import { t } from '../../i18n'

const props = defineProps<{ topicId: string }>()

const { $api } = useNuxtApp()

const { state, loadingMore, moreFailed, loadMore, retry } = await useFeed(
  `topic:${props.topicId}`,
  (cursor) => loadBlogFeed($api, props.topicId, cursor),
)
</script>

<template>
  <PullToRefresh :refresh="retry">
    <FeedView
      :label="t('topic.posts')"
      :state="state"
      :loading-more="loadingMore"
      :more-failed="moreFailed"
      :empty-text="t('topic.noPosts')"
      @retry="retry()"
      @more="loadMore()"
    >
      <template #empty-actions>
        <NuxtLink
          :to="{ path: '/write', query: { topic: topicId } }"
          class="topic-feed__write"
          data-testid="topic-write"
          >{{ t('topic.writeHere') }}</NuxtLink
        >
      </template>
    </FeedView>
  </PullToRefresh>
</template>

<style scoped>
/* The look of a small secondary UiButton, for an action that leads to another page. */
.topic-feed__write {
  display: inline-flex;
  align-items: center;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  color: var(--color-text);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.topic-feed__write:hover {
  background: var(--color-surface-subtle);
}
</style>
