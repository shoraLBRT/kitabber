<script setup lang="ts">
// The card of a post in a feed (E08-02, FR-FEED, docs/ui/feed.md, ADR-0052): the author with the avatar, the topic, the
// date (a link to the post), the title and the post's blocks drawn by PostBlocks. A short post is shown whole; a
// longread shows its first blocks (feedPreview) and «Показать полностью» opens the rest in place. Under it, the best
// comment of a post with comments (E08-15, ADR-0062). The reactions, the counters and the «…» menu come with their own
// issues (FR-REACT, E08-06, FR-POST); the best comment stays the card's last block, under the counters. The first
// counter is the views (E07-06, ADR-0071), and a card that half stayed on screen for a second is reported as seen.
// «В закладки» ends the counters' row (E10-07, ADR-0113); the author sees beside it how many people keep the post.
// A repost (E10-08, ADR-0117) is the reposter, «сделал репост», the date and their text over the original's card in a
// frame: the original's author, topic, title and blocks, leading to the original; the counters are the original's.
import { computed, ref } from 'vue'
import { Eye } from 'lucide-vue-next'
import FeedBestComment from './FeedBestComment.vue'
import BookmarkButton from '../bookmarks/BookmarkButton.vue'
import type { Bookmarks } from '../../composables/useBookmarks'
import { useCardImpression } from '../../composables/useViews'
import PostBlocks from '../post/PostBlocks.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import { feedCommentPath, feedItemPath, feedPreview, type FeedPost } from '../../api/feeds'
import { blogTitle } from '../../api/profiles'
import { isRepost } from '../../api/reposts'
import { subsitePath } from '../../api/posts'
import { locale, t } from '../../i18n'
import { isolateRtlText } from '../../utils/bidi'

const props = withDefaults(
  defineProps<{
    item: FeedPost
    /** The person's bookmarks (E10-07): «В закладки» among the counters; `null` shows none. */
    bookmarks?: Bookmarks | null
  }>(),
  { bookmarks: null },
)

// A repost shows the original inside: what the card's title, blocks and counters are about.
const repost = computed(() => (isRepost(props.item) ? props.item : null))
const shownItem = computed(() =>
  repost.value
    ? {
        post: repost.value.original.post,
        author: repost.value.original.author,
        topic: repost.value.original.topic,
      }
    : props.item,
)
const shownPost = computed(() => shownItem.value.post)

const bookmark = computed(() => props.bookmarks?.of('post', shownPost.value.id) ?? null)
const bookmarkBusy = computed(() => props.bookmarks?.isBusy('post', shownPost.value.id) === true)

// A fixed time zone keeps the server and the browser on the same text, so hydration does not differ.
const dateFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'long',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'UTC',
})

const expanded = ref(false)

const path = computed(() => feedItemPath(shownItem.value))
const author = computed(() => shownItem.value.author)
const authorName = computed(() => (author.value ? blogTitle(author.value) : t('common.author')))
const formatted = (at: string | null | undefined) => (at ? dateFormat.format(new Date(at)) : '')
const date = computed(() => formatted(shownPost.value.publishedAt))
const reposter = computed(() => props.item.author)
const reposterName = computed(() => (reposter.value ? blogTitle(reposter.value) : t('common.author')))
const preview = computed(() => feedPreview(shownPost.value.body))
const shown = computed(() => (expanded.value ? shownPost.value.body : preview.value.document))
const views = computed(() => t('views.count', { count: shownPost.value.viewCount }))

const card = ref<HTMLElement | null>(null)
useCardImpression(card, () => shownPost.value.id)
</script>

<template>
  <article ref="card" class="feed-card" data-testid="feed-card">
    <template v-if="repost">
      <header class="feed-card__meta" data-testid="feed-card-repost">
        <NuxtLink v-if="reposter" :to="subsitePath(reposter)" class="feed-card__author">
          <UiAvatar :name="reposterName" :sources="reposter.avatarSources" size="sm" decorative />
          <span class="feed-card__name">{{ reposterName }}</span>
        </NuxtLink>
        <span class="feed-card__muted">{{ t('reposts.reposted') }}</span>
        <template v-if="item.post.publishedAt">
          <span class="feed-card__muted" aria-hidden="true">·</span>
          <time class="feed-card__muted" :datetime="item.post.publishedAt">{{
            formatted(item.post.publishedAt)
          }}</time>
        </template>
      </header>
      <p
        v-if="item.post.repostText"
        class="feed-card__repost-text"
        dir="auto"
        data-testid="feed-card-repost-text"
      >
        {{ item.post.repostText }}
      </p>
    </template>
    <div :class="['feed-card__body', { 'feed-card__body--original': repost }]">
      <header class="feed-card__meta">
        <NuxtLink
          v-if="author"
          :to="subsitePath(author)"
          class="feed-card__author"
          data-testid="feed-card-author"
        >
          <UiAvatar :name="authorName" :sources="author.avatarSources" size="sm" decorative />
          <span class="feed-card__name">{{ authorName }}</span>
        </NuxtLink>
        <template v-if="shownItem.topic">
          <span class="feed-card__muted" aria-hidden="true">·</span>
          <NuxtLink
            :to="subsitePath(shownItem.topic)"
            class="feed-card__topic"
            data-testid="feed-card-topic"
            >{{ blogTitle(shownItem.topic) }}</NuxtLink
          >
        </template>
        <template v-if="shownPost.publishedAt">
          <span class="feed-card__muted" aria-hidden="true">·</span>
          <NuxtLink :to="path" class="feed-card__muted feed-card__date" data-testid="feed-card-date">
            <time :datetime="shownPost.publishedAt">{{ date }}</time>
          </NuxtLink>
        </template>
      </header>

      <NuxtLink v-if="shownPost.title" :to="path" class="feed-card__link" data-testid="feed-card-title">
        <h2 class="feed-card__title" dir="auto">{{ isolateRtlText(shownPost.title) }}</h2>
      </NuxtLink>

      <PostBlocks :document="shown" :images="shownPost.images" :previews="shownPost.imagePreviews" />

      <button
        v-if="preview.cut && !expanded"
        type="button"
        class="feed-card__more"
        data-testid="feed-card-more"
        @click="expanded = true"
      >
        {{ t('actions.showAll') }}
      </button>
    </div>

    <footer class="feed-card__counters">
      <span class="feed-card__counter" data-testid="feed-card-views">
        <Eye class="feed-card__icon" aria-hidden="true" />
        {{ views }}
      </span>
      <BookmarkButton
        v-if="bookmark"
        class="feed-card__bookmark"
        :state="bookmark"
        :busy="bookmarkBusy"
        testid="feed-card-bookmark"
        @press="bookmarks?.toggle('post', shownPost.id)"
      />
    </footer>

    <FeedBestComment
      v-if="item.bestComment"
      :comment="item.bestComment"
      :to="feedCommentPath(item, item.bestComment.id)"
    />
  </article>
</template>

<style scoped>
.feed-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.feed-card__body {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

/* The original inside a repost, in a frame. */
.feed-card__body--original {
  padding: var(--space-3) var(--space-4);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-lg);
}

.feed-card__repost-text {
  margin: 0;
  white-space: pre-line;
  overflow-wrap: anywhere;
}

.feed-card__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
  font-size: var(--font-size-sm);
}

.feed-card__author {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.feed-card__name {
  overflow-wrap: anywhere;
}

.feed-card__topic {
  color: var(--color-text);
  text-decoration: none;
  overflow-wrap: anywhere;
}

.feed-card__author:hover .feed-card__name,
.feed-card__topic:hover,
.feed-card__date:hover {
  text-decoration: underline;
}

.feed-card__muted {
  color: var(--color-muted);
}

.feed-card__date {
  text-decoration: none;
}

.feed-card__link {
  color: inherit;
  text-decoration: none;
}

.feed-card__link:hover .feed-card__title {
  text-decoration: underline;
}

.feed-card__title {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
  overflow-wrap: anywhere;
}

.feed-card__more {
  align-self: flex-start;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-accent);
  font: inherit;
  font-weight: var(--font-weight-medium);
  cursor: pointer;
}

.feed-card__more:hover {
  text-decoration: underline;
}

.feed-card__counters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-4);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.feed-card__counter {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
}

/* The bookmark ends the row. */
.feed-card__bookmark {
  margin-left: auto;
}

.feed-card__icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  flex: none;
}
</style>
