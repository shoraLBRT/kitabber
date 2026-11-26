<script setup lang="ts">
// The card of a post in a feed (E08-02, FR-FEED, docs/ui/feed.md, ADR-0052): the author with the avatar, the topic, the
// date (a link to the post), the title and the post's blocks drawn by PostBlocks. A short post is shown whole; a
// longread shows its first blocks (feedPreview) and «Показать полностью» opens the rest in place. Under it, the best
// comment of a post with comments (E08-15, ADR-0062). The reactions, the counters and the «…» menu come with their own
// issues (FR-REACT, E08-06, FR-POST); the best comment stays the card's last block, under the counters to come.
import { computed, ref } from 'vue'
import FeedBestComment from './FeedBestComment.vue'
import PostBlocks from '../post/PostBlocks.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import { feedCommentPath, feedItemPath, feedPreview, type FeedPost } from '../../api/feeds'
import { blogTitle } from '../../api/profiles'
import { subsitePath } from '../../api/posts'
import { locale, t } from '../../i18n'

const props = defineProps<{ item: FeedPost }>()

// A fixed time zone keeps the server and the browser on the same text, so hydration does not differ.
const dateFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'long',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'UTC',
})

const expanded = ref(false)

const path = computed(() => feedItemPath(props.item))
const author = computed(() => props.item.author)
const authorName = computed(() => (author.value ? blogTitle(author.value) : t('common.author')))
const date = computed(() =>
  props.item.post.publishedAt ? dateFormat.format(new Date(props.item.post.publishedAt)) : '',
)
const preview = computed(() => feedPreview(props.item.post.body))
const shown = computed(() => (expanded.value ? props.item.post.body : preview.value.document))
</script>

<template>
  <article class="feed-card" data-testid="feed-card">
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
      <template v-if="item.topic">
        <span class="feed-card__muted" aria-hidden="true">·</span>
        <NuxtLink :to="subsitePath(item.topic)" class="feed-card__topic" data-testid="feed-card-topic">{{
          blogTitle(item.topic)
        }}</NuxtLink>
      </template>
      <template v-if="item.post.publishedAt">
        <span class="feed-card__muted" aria-hidden="true">·</span>
        <NuxtLink :to="path" class="feed-card__muted feed-card__date" data-testid="feed-card-date">
          <time :datetime="item.post.publishedAt">{{ date }}</time>
        </NuxtLink>
      </template>
    </header>

    <NuxtLink v-if="item.post.title" :to="path" class="feed-card__link" data-testid="feed-card-title">
      <h2 class="feed-card__title">{{ item.post.title }}</h2>
    </NuxtLink>

    <PostBlocks :document="shown" :images="item.post.images" :previews="item.post.imagePreviews" />

    <button
      v-if="preview.cut && !expanded"
      type="button"
      class="feed-card__more"
      data-testid="feed-card-more"
      @click="expanded = true"
    >
      {{ t('actions.showAll') }}
    </button>

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
</style>
