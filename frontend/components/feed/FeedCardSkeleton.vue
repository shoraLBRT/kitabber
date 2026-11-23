<script setup lang="ts">
// The skeleton of a feed card (E02-07, docs/ui/feed.md, ADR-0057): the avatar and the line of the author, the title
// and two lines of text, laid out as FeedCard lays them out and sized by the same tokens, so the card takes the
// skeleton's place without a jump. The feed shows three on the first load and one under the list for the next page.
import UiSkeleton from '../ui/UiSkeleton.vue'
</script>

<template>
  <div class="feed-card-skeleton" data-testid="feed-card-skeleton">
    <div class="feed-card-skeleton__meta" data-testid="feed-card-skeleton-meta">
      <UiSkeleton shape="avatar" size="sm" />
      <UiSkeleton size="sm" width="40%" />
    </div>
    <UiSkeleton size="xl" width="70%" data-testid="feed-card-skeleton-title" />
    <div class="feed-card-skeleton__text">
      <UiSkeleton size="reading" />
      <UiSkeleton size="reading" width="80%" />
    </div>
  </div>
</template>

<style scoped>
/* The gaps and the meta line of FeedCard. */
.feed-card-skeleton {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.feed-card-skeleton__meta {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.feed-card-skeleton__text {
  display: flex;
  flex-direction: column;
}
</style>
