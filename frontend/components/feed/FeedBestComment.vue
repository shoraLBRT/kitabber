<script setup lang="ts">
// The best comment under a post's card in a feed (E08-15, FR-FEED, docs/ui/feed.md, ADR-0062): the avatar, the
// author's name and the first lines of the text, cut with an ellipsis. The whole block is one link to the comment on
// the post's page. The server chooses the comment (the most liked shown one, the earlier on a tie) for the whole page.
import { computed } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import type { FeedComment } from '../../api/feeds'
import { blogTitle } from '../../api/profiles'
import { t } from '../../i18n'

const props = defineProps<{ comment: FeedComment; to: string }>()

// A native comment names its author's blog; a guest one (FR-TG-6) the outside author.
const name = computed(() =>
  props.comment.author
    ? blogTitle(props.comment.author)
    : props.comment.guestName?.trim() || t('common.author'),
)
</script>

<template>
  <NuxtLink :to="to" class="feed-comment" data-testid="feed-best-comment">
    <UiAvatar :name="name" :sources="comment.author?.avatarSources ?? []" size="sm" decorative />
    <span class="feed-comment__body">
      <span class="feed-comment__author" data-testid="feed-best-comment-author">{{ name }}</span>
      <span class="feed-comment__text" data-testid="feed-best-comment-text">{{ comment.text }}</span>
    </span>
  </NuxtLink>
</template>

<style scoped>
.feed-comment {
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-3);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
  color: var(--color-text);
  text-decoration: none;
}

.feed-comment:hover .feed-comment__author {
  text-decoration: underline;
}

.feed-comment__body {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.feed-comment__author {
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  overflow-wrap: anywhere;
}

/* The first lines only: three on any screen, the rest cut with an ellipsis. */
.feed-comment__text {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 3;
  line-clamp: 3;
  white-space: pre-line;
  overflow-wrap: anywhere;
}
</style>
