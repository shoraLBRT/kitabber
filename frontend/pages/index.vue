<script setup lang="ts">
// Main feed (default "Популярное", docs/ui/feed.md). The feed itself comes with FR-FEED; until then the central
// column shows the empty state of the shared FR-UI states. Above it on a desktop is the quick post (E05-29,
// ADR-0055); on a phone the floating «Написать» opens it. A signed-in person who chose another feed for the home page
// in «Ленты» (E08-13, ADR-0110) is sent to it: «Свежее» at /new, «Моя лента» at /my; the menu still leads to every feed.
import QuickPost from '~/components/feed/QuickPost.vue'
import { homeFeedPath, ownPreferences, type Preferences } from '~/api/preferences'
import { t } from '../i18n'

const account = useAccount()
if (account.value.signedIn) {
  const { $api } = useNuxtApp()
  const saved = import.meta.server
    ? await ownPreferences($api)
    : await $fetch<Preferences>('/bff/preferences').catch(() => null)
  const home = homeFeedPath(saved)
  if (home) await navigateTo(home, { replace: true, redirectCode: 302 })
}
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
