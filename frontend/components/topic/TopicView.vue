<script setup lang="ts">
// The topic page (E11-01, FR-TOPIC, docs/ui/topic.md, ADR-0082): the header of the topic — cover, avatar, name,
// description and its owner — and its posts below it, which the page passes as the slot. States: loading (a skeleton of
// the header), a failure with «Повторить», and the topic. A missing topic is the page's 404 (error.vue). The subscriber
// count and «Подписаться» are BlogSubscription.vue (E11-03), its state held by the page. Notifications, the «Комментарии» tab, the rules and moderators come with their own E11 issues; changing the cover of
// a topic comes with the topic's management (E11-12, #114), so the cover is only shown here.
import { computed } from 'vue'
import BlogCover from '../blog/BlogCover.vue'
import BlogSubscription from '../blog/BlogSubscription.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiState from '../ui/UiState.vue'
import { blogPath, blogTitle, type Profile } from '../../api/profiles'
import { subscriptionOf, type SubscriptionView } from '../../api/subscriptions'
import { t } from '../../i18n'

const props = defineProps<{
  state: { status: 'loading' } | { status: 'error' } | { status: 'found'; profile: Profile }
  /** The subscription as the page holds it (useSubscription); the profile's own numbers without it. */
  subscription?: { state: SubscriptionView; busy: boolean }
}>()

defineEmits<{ retry: []; subscribe: [] }>()

const topic = computed(() => (props.state.status === 'found' ? props.state.profile : null))
const shownSubscription = computed(() =>
  topic.value ? (props.subscription?.state ?? subscriptionOf(topic.value)) : null,
)
const title = computed(() => (topic.value ? blogTitle(topic.value) : ''))
const owner = computed(() => topic.value?.owner ?? null)
</script>

<template>
  <section v-if="state.status === 'loading'" class="topic" aria-busy="true" data-testid="topic-loading">
    <span class="visually-hidden" role="status">{{ t('topic.loading') }}</span>
    <div class="topic__card">
      <div class="topic__cover" />
      <div class="topic__head">
        <div class="topic__skeleton topic__skeleton--avatar" />
        <div class="topic__skeleton topic__skeleton--line" />
        <div class="topic__skeleton topic__skeleton--short" />
      </div>
    </div>
  </section>

  <UiState
    v-else-if="state.status === 'error'"
    tone="error"
    :title="t('topic.loadFailed')"
    heading="h1"
    data-testid="topic-error"
  >
    {{ t('topic.loadFailedText') }}
    <template #actions>
      <UiButton variant="secondary" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
    </template>
  </UiState>

  <div v-else-if="topic" class="topic" data-testid="topic">
    <header class="topic__card">
      <BlogCover :cover="topic.cover" />
      <div class="topic__head">
        <UiAvatar
          class="topic__avatar"
          :name="title"
          :src="topic.avatarUrl ?? undefined"
          :sources="topic.avatarSources"
          size="lg"
          decorative
        />
        <h1 class="topic__title" data-testid="topic-title">{{ title }}</h1>
        <p v-if="topic.description" class="topic__description" data-testid="topic-description">
          {{ topic.description }}
        </p>
        <p v-if="owner" class="topic__muted" data-testid="topic-owner">
          {{ t('topic.owner') }}
          <NuxtLink :to="blogPath(owner)" class="topic__owner">{{ blogTitle(owner) }}</NuxtLink>
        </p>
        <BlogSubscription
          v-if="shownSubscription"
          :state="shownSubscription"
          :busy="props.subscription?.busy"
          @toggle="$emit('subscribe')"
        />
      </div>
    </header>

    <slot :profile="topic" />
  </div>
</template>

<style scoped>
.topic {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.topic__card {
  overflow: hidden;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

/* The proportion of the cover (utils/cover-position.ts, coverRatio), so the skeleton is the size of the cover. */
.topic__cover {
  aspect-ratio: 10 / 3;
  background: var(--color-surface-subtle);
}

.topic__head {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  padding: 0 var(--space-5) var(--space-5);
}

.topic__avatar {
  position: relative;
  margin-top: calc(-1 * var(--space-8));
  border: var(--size-focus-ring) solid var(--color-surface);
  border-radius: var(--radius-full);
}

.topic__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
  overflow-wrap: anywhere;
}

.topic__description {
  margin: 0;
  overflow-wrap: anywhere;
}

.topic__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.topic__owner {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
}

.topic__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.topic__skeleton--avatar {
  width: var(--space-16);
  height: var(--space-16);
  margin-top: calc(-1 * var(--space-8));
  border-radius: var(--radius-full);
}

.topic__skeleton--line {
  width: 50%;
  height: var(--space-6);
}

.topic__skeleton--short {
  width: 30%;
  height: var(--space-4);
}
</style>
