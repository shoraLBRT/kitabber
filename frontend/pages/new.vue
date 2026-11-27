<script setup lang="ts">
// The fresh feed «Свежее» (E08-01, FR-FEED, docs/ui/feed.md) at `/new`: every published post, newest first. The first
// page is rendered on the server; later pages load as the reader scrolls, and «назад» from a post comes back to the
// same place (E08-03, useFeed, ADR-0051). The feed is public, so the browser asks the API directly. Above it on a
// desktop is the quick post (E05-29, ADR-0055); on a phone the floating «Написать» opens it. Above the feed is the
// rating filter (E08-07, ADR-0106): the server applies the threshold, and the choice is remembered (useFreshRating).
// New posts are announced by «Показать новые» and put on top only when it is pressed (E08-08, useNewPosts, ADR-0108).
import FeedView from '~/components/feed/FeedView.vue'
import FreshRatingFilter from '~/components/feed/FreshRatingFilter.vue'
import NewPostsBanner from '~/components/feed/NewPostsBanner.vue'
import QuickPost from '~/components/feed/QuickPost.vue'
import UiButton from '~/components/ui/UiButton.vue'
import { loadFreshFeed, type FreshRating } from '~/api/feeds'
import { t } from '../i18n'

const { $api } = useNuxtApp()

const { rating, choose } = await useFreshRating()

const { state, posts, cursor, loadingMore, moreFailed, loadMore, retry } = await useFeed('fresh', (cursor) =>
  loadFreshFeed($api, cursor, rating.value),
)

// «Показать новые» (E08-08, ADR-0108): the first page is read again with the same threshold, so only the new posts that
// pass it are counted; new posts never enter the feed until the banner is pressed.
const newPosts = useNewPosts(posts, cursor, () => loadFreshFeed($api, null, rating.value))

// Another threshold is another feed: it is read again from its first page, and what was counted for the old one goes.
watch(rating, () => {
  newPosts.reset()
  void retry()
})

function chooseRating(next: FreshRating) {
  void choose(next)
}

useHead({ title: t('nav.new') })
</script>

<template>
  <div class="feed-page">
    <QuickPost class="feed-page__quick-post" />
    <h1 class="feed-page__title">{{ t('nav.new') }}</h1>
    <FreshRatingFilter :current="rating" @choose="chooseRating" />
    <NewPostsBanner :count="newPosts.count.value" :more="newPosts.more.value" @show="newPosts.show()" />
    <FeedView
      :label="t('nav.new')"
      :state="state"
      :loading-more="loadingMore"
      :more-failed="moreFailed"
      :empty-text="rating === null ? undefined : t('feed.freshRatingEmpty')"
      @retry="retry()"
      @more="loadMore()"
    >
      <template v-if="rating !== null" #empty-actions>
        <UiButton variant="secondary" data-testid="fresh-rating-all" @click="chooseRating(null)">
          {{ t('feed.freshRatingShowAll') }}
        </UiButton>
      </template>
    </FeedView>
  </div>
</template>

<style scoped>
.feed-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.feed-page__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

@media (max-width: 639px) {
  .feed-page__quick-post {
    display: none;
  }
}
</style>
