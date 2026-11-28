<script setup lang="ts">
// The blog page (E04-02, FR-PROFILE, docs/ui/blog.md): the header of the blog and its tabs «Посты» and «Комментарии»,
// each at its own address, so the tab content is the slot. States: loading (a skeleton of the header and the list),
// not found, a failure with «Повторить», and the blog itself. The owner also sees the registration date and the
// management buttons: «Ред.» and «Настройки» open the blog settings (E04-03, /settings/blog); statistics comes with
// its own issue, so its button is there but disabled. The subscriber numbers and «Подписаться» are BlogSubscription.vue
// (E11-03); the page passes their state. A signed-in visitor of someone else's personal blog blocks its owner with
// «Заблокировать» (E11-06, BlogBlock.vue); the other visitor actions (message, report) come with FR-MSG and FR-MOD.
// The cover (E04-04, BlogCover.vue) is shown to everyone; the owner's page passes coverActions to change it.
import { computed } from 'vue'
import BlogBlock from './BlogBlock.vue'
import BlogCover, { type CoverActions } from './BlogCover.vue'
import BlogSubscription from './BlogSubscription.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import { blogTitle, type Profile, type ProfileLookup } from '../../api/profiles'
import { subscriptionOf, type SubscriptionView } from '../../api/subscriptions'
import { locale, t } from '../../i18n'

export type BlogTab = 'posts' | 'comments'

const props = defineProps<{
  state: ProfileLookup | { status: 'loading' }
  /** The blog's address, e.g. /u/anna; the tabs live under it. */
  base: string
  tab: BlogTab
  /** What the owner can do with the cover; only the owner's page passes it. */
  coverActions?: CoverActions
  /** The subscription as the page holds it (useSubscription); the profile's own numbers without it. */
  subscription?: { state: SubscriptionView; busy: boolean }
  /** The reader is signed in: someone else's personal blog then offers «Заблокировать» (E11-06). */
  signedIn?: boolean
}>()

defineEmits<{
  retry: []
  saved: [profile: Profile]
  unauthorized: []
  subscribe: []
  bell: []
  blocked: [blocked: boolean]
}>()

const tabs = computed(() => [
  { id: 'posts' as const, label: t('blog.posts'), to: props.base },
  { id: 'comments' as const, label: t('blog.comments'), to: `${props.base}/comments` },
])

const found = computed(() => (props.state.status === 'found' ? props.state : null))
const title = computed(() => (found.value ? blogTitle(found.value.profile) : ''))
// The owner a signed-in reader may block: only a person's blog, never the reader's own (ADR-0091).
const blockable = computed(() => {
  const profile = found.value?.profile
  return props.signedIn && profile && !found.value?.owner && profile.type === 'personal' && profile.accountId
    ? { accountId: profile.accountId, blocked: profile.blocked }
    : null
})
const shownSubscription = computed(() =>
  found.value ? (props.subscription?.state ?? subscriptionOf(found.value.profile)) : null,
)

// A fixed time zone keeps the server and the browser on the same day, so hydration does not differ.
const registered = computed(() => {
  const at = found.value?.profile.registeredAt
  if (!at) return null
  return new Intl.DateTimeFormat(locale, {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(at))
})
</script>

<template>
  <section v-if="state.status === 'loading'" class="blog" aria-busy="true" data-testid="blog-loading">
    <span class="visually-hidden" role="status">{{ t('blog.loading') }}</span>
    <div class="blog__card">
      <div class="blog__cover" />
      <div class="blog__head">
        <div class="blog__skeleton blog__skeleton--avatar" />
        <div class="blog__skeleton blog__skeleton--line" />
        <div class="blog__skeleton blog__skeleton--short" />
      </div>
    </div>
    <div v-for="n in 3" :key="n" class="blog__skeleton blog__skeleton--card" />
  </section>

  <section
    v-else-if="state.status === 'not-found'"
    class="blog blog__card blog__message"
    data-testid="blog-not-found"
  >
    <h1 class="blog__title">{{ t('blog.notFound') }}</h1>
    <p class="blog__muted">{{ t('blog.notFoundText') }}</p>
    <NuxtLink to="/">{{ t('common.home') }}</NuxtLink>
  </section>

  <section
    v-else-if="state.status === 'error'"
    class="blog blog__card blog__message"
    data-testid="blog-error"
  >
    <h1 class="blog__title">{{ t('blog.loadFailed') }}</h1>
    <p class="blog__muted" role="alert">{{ t('blog.loadFailedText') }}</p>
    <UiButton variant="secondary" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
  </section>

  <div v-else-if="found" class="blog" data-testid="blog">
    <header class="blog__card">
      <BlogCover
        :cover="found.profile.cover"
        :actions="found.owner ? coverActions : undefined"
        @saved="$emit('saved', $event)"
        @unauthorized="$emit('unauthorized')"
      />
      <div class="blog__head">
        <UiAvatar
          class="blog__avatar"
          :name="title"
          :src="found.profile.avatarUrl ?? undefined"
          :sources="found.profile.avatarSources"
          size="lg"
          decorative
        />
        <h1 class="blog__title">{{ title }}</h1>
        <p v-if="found.profile.nickname" class="blog__muted" data-testid="blog-nickname">
          @{{ found.profile.nickname }}
        </p>
        <p v-if="found.profile.description" class="blog__description">{{ found.profile.description }}</p>
        <BlogSubscription
          v-if="shownSubscription"
          :state="shownSubscription"
          :subscription-count="found.profile.subscriptionCount"
          :blog-id="found.profile.id"
          :own="found.owner"
          :busy="props.subscription?.busy"
          @toggle="$emit('subscribe')"
          @bell="$emit('bell')"
        />
        <BlogBlock
          v-if="blockable"
          :account-id="blockable.accountId"
          :name="title"
          :blocked="blockable.blocked"
          @change="$emit('blocked', $event)"
        />
        <template v-if="found.owner">
          <p class="blog__muted" data-testid="blog-registered">
            {{ t('blog.registered', { date: registered ?? '' }) }}
          </p>
          <div class="blog__actions" role="group" :aria-label="t('blog.manage')">
            <NuxtLink to="/settings/blog" class="blog__action">{{ t('blog.edit') }}</NuxtLink>
            <UiButton variant="secondary" size="sm" disabled aria-describedby="blog-soon">{{
              t('blog.stats')
            }}</UiButton>
            <NuxtLink to="/settings/blog" class="blog__action">{{ t('blog.settings') }}</NuxtLink>
          </div>
          <p id="blog-soon" class="blog__muted blog__note">{{ t('blog.statsSoon') }}</p>
        </template>
      </div>
    </header>

    <nav class="blog__tabs" :aria-label="t('blog.sections')">
      <NuxtLink
        v-for="item in tabs"
        :key="item.id"
        :to="item.to"
        class="blog__tab"
        :aria-current="item.id === tab ? 'page' : undefined"
      >
        {{ item.label }}
      </NuxtLink>
    </nav>

    <section class="blog__panel" :aria-label="tabs.find((item) => item.id === tab)!.label">
      <slot :profile="found.profile" :owner="found.owner" />
    </section>
  </div>
</template>

<style scoped>
.blog {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.blog__card {
  overflow: hidden;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.blog__message {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  padding: var(--space-5);
}

/* The proportion of the cover (utils/cover-position.ts, coverRatio), so the skeleton is the size of the cover. */
.blog__cover {
  aspect-ratio: 10 / 3;
  background: var(--color-surface-subtle);
}

.blog__head {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  padding: 0 var(--space-5) var(--space-5);
}

.blog__avatar {
  position: relative;
  margin-top: calc(-1 * var(--space-8));
  border: var(--size-focus-ring) solid var(--color-surface);
  border-radius: var(--radius-full);
}

.blog__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
  overflow-wrap: anywhere;
}

.blog__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.blog__description {
  margin: 0;
  overflow-wrap: anywhere;
}

.blog__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

/* The look of a small secondary UiButton, for the actions that lead to another page. */
.blog__action {
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

.blog__action:hover {
  background: var(--color-surface-subtle);
}

.blog__note {
  font-size: var(--font-size-xs);
}

.blog__tabs {
  display: flex;
  gap: var(--space-1);
  padding: 0 var(--space-3);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.blog__tab {
  display: inline-flex;
  align-items: center;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border-bottom: var(--size-focus-ring) solid transparent;
  color: var(--color-muted);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.blog__tab:hover {
  color: var(--color-text);
}

.blog__tab[aria-current='page'] {
  border-bottom-color: var(--color-accent);
  color: var(--color-text);
}

.blog__tab:focus-visible {
  outline-offset: calc(-1 * var(--size-focus-ring));
}

.blog__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.blog__skeleton--avatar {
  width: var(--space-16);
  height: var(--space-16);
  margin-top: calc(-1 * var(--space-8));
  border-radius: var(--radius-full);
}

.blog__skeleton--line {
  width: 50%;
  height: var(--space-6);
}

.blog__skeleton--short {
  width: 30%;
  height: var(--space-4);
}

.blog__skeleton--card {
  height: calc(var(--space-16) * 2);
  border-radius: var(--radius-lg);
}
</style>
