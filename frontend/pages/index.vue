<script setup lang="ts">
// Main feed (default "Популярное", docs/ui/feed.md). The feed itself comes with FR-FEED; until then the central
// column shows the empty state of the shared FR-UI states. Above it on a desktop is the quick post (E05-29,
// ADR-0055); on a phone the floating «Написать» opens it.
import QuickPost from '~/components/feed/QuickPost.vue'
import { t } from '../i18n'
</script>

<template>
  <div class="feed-page">
    <QuickPost class="feed-page__quick-post" />
    <section class="feed-placeholder" aria-labelledby="feed-title">
      <h1 id="feed-title" class="feed-placeholder__title">{{ t('nav.popular') }}</h1>
      <p class="feed-placeholder__text">{{ t('pages.popularPlaceholder') }}</p>
    </section>
  </div>
</template>

<style scoped>
.feed-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.feed-placeholder {
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.feed-placeholder__title {
  margin: 0 0 var(--space-2);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.feed-placeholder__text {
  margin: 0;
  color: var(--color-muted);
}

@media (max-width: 639px) {
  .feed-placeholder {
    border-radius: 0;
  }

  .feed-page__quick-post {
    display: none;
  }
}
</style>
