<script setup lang="ts">
// The top of a period in «Популярном» (E08-06, FR-FEED, ADR-0104) at /popular/<period> — day, week, month, year, all:
// the posts published within the period by rating, highest first, page by page like the other feeds (useFeed,
// ADR-0051). Each period has its own address and its own history entry; the switch above the feed leads between them.
// The feed is public, so the first page is rendered on the server and the browser asks the API directly. An address
// with another period is the usual 404 page. The «Популярное» feed by hotness at /popular is E08-05a (#406).
import FeedView from '~/components/feed/FeedView.vue'
import TopPeriodSwitch from '~/components/feed/TopPeriodSwitch.vue'
import { isTopPeriod, loadTopFeed, type TopPeriod } from '~/api/feeds'
import { t } from '../../i18n'

// Another period is another page: the feed of one period is never shown under another's address.
definePageMeta({ key: (route) => route.path })

const route = useRoute()
const { $api } = useNuxtApp()
const segment = String(route.params.period)

if (!isTopPeriod(segment)) throw createError({ statusCode: 404, statusMessage: 'Not Found', fatal: true })
const period: TopPeriod = segment

const titles = {
  day: t('feed.topDay'),
  week: t('feed.topWeek'),
  month: t('feed.topMonth'),
  year: t('feed.topYear'),
  all: t('feed.topAll'),
} satisfies Record<TopPeriod, string>

const { state, loadingMore, moreFailed, loadMore, retry } = await useFeed(`top:${period}`, (cursor) =>
  loadTopFeed($api, period, cursor),
)

useHead({ title: `${t('nav.popular')} · ${titles[period].toLowerCase()}` })
</script>

<template>
  <div class="top-page">
    <h1 class="top-page__title">{{ t('nav.popular') }}</h1>
    <TopPeriodSwitch :current="period" />
    <FeedView
      :label="titles[period]"
      :state="state"
      :loading-more="loadingMore"
      :more-failed="moreFailed"
      :empty-text="t('feed.topEmpty')"
      @retry="retry()"
      @more="loadMore()"
    >
      <template #empty-actions>
        <NuxtLink to="/new" class="top-page__fresh" data-testid="top-fresh">{{
          t('feed.topFresh')
        }}</NuxtLink>
      </template>
    </FeedView>
  </div>
</template>

<style scoped>
.top-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.top-page__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.top-page__fresh {
  color: var(--color-accent);
  font-weight: var(--font-weight-medium);
}
</style>
