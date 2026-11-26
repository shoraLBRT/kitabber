<script setup lang="ts">
// What an empty «Моя лента» offers (E08-04, ADR-0089): a few topics to open and subscribe to on their page, and the
// fresh feed to find authors in. The page reads the topics (GET /api/v1/topics); with none, only the fresh feed is
// offered. The catalogue of topics is E11-02 (#78).
import { computed } from 'vue'
import type { Topic } from '../../api/topics'
import { t } from '../../i18n'

/** How many topics the empty feed offers. */
const shownTopics = 6

const props = defineProps<{ topics: Pick<Topic, 'id' | 'name' | 'nickname'>[] }>()

const shown = computed(() => props.topics.slice(0, shownTopics))
</script>

<template>
  <div class="my-feed-suggestions" data-testid="my-feed-suggestions">
    <ul v-if="shown.length > 0" class="my-feed-suggestions__topics" :aria-label="t('feed.mySuggestions')">
      <li v-for="topic in shown" :key="topic.id">
        <NuxtLink :to="`/${topic.nickname}`" class="my-feed-suggestions__link" data-testid="my-feed-topic">{{
          topic.name
        }}</NuxtLink>
      </li>
    </ul>
    <NuxtLink to="/new" class="my-feed-suggestions__link" data-testid="my-feed-fresh">{{
      t('feed.myFindAuthors')
    }}</NuxtLink>
  </div>
</template>

<style scoped>
.my-feed-suggestions {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
}

.my-feed-suggestions__topics {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

/* The look of a small secondary UiButton, for an action that leads to another page. */
.my-feed-suggestions__link {
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

.my-feed-suggestions__link:hover {
  background: var(--color-surface-subtle);
}
</style>
