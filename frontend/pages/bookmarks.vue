<script setup lang="ts">
// The section «Закладки» (E04-12, FR-PROFILE, FR-REACT, ADR-0116) at `/bookmarks`: the person's own bookmarks, newest
// first, in the tabs «Посты» (`/bookmarks`) and «Комментарии» (`/bookmarks/comments`). The tabs are child pages, as
// the blog's are, so they switch without a reload. Only the owner sees the section: the list is read with their token
// and a guest is asked to sign in (the sign-in window, ADR-0032).
import UiButton from '~/components/ui/UiButton.vue'
import UiState from '~/components/ui/UiState.vue'
import { bookmarkedCommentsPath, bookmarksPath } from '~/api/bookmark-section'
import { t } from '../i18n'

useHead({ title: t('nav.bookmarks') })

const route = useRoute()
const account = useAccount()
const authWindow = useAuthWindow()

const tabs = [
  { id: 'posts', label: t('bookmarkSection.posts'), to: bookmarksPath },
  { id: 'comments', label: t('bookmarkSection.comments'), to: bookmarkedCommentsPath },
]
const tab = computed(() => (route.path.replace(/\/+$/, '').endsWith('/comments') ? 'comments' : 'posts'))

function signIn() {
  authWindow.value = { open: true, mode: 'sign-in' }
}
</script>

<template>
  <section class="bookmarks" aria-labelledby="bookmarks-title" data-testid="bookmarks">
    <h1 id="bookmarks-title" class="bookmarks__title">{{ t('nav.bookmarks') }}</h1>
    <template v-if="account.signedIn">
      <nav class="bookmarks__tabs" :aria-label="t('bookmarkSection.tabs')">
        <NuxtLink
          v-for="item in tabs"
          :key="item.id"
          :to="item.to"
          class="bookmarks__tab"
          :aria-current="item.id === tab ? 'page' : undefined"
          >{{ item.label }}</NuxtLink
        >
      </nav>
      <NuxtPage />
    </template>
    <UiState v-else data-testid="bookmarks-guest">
      {{ t('bookmarkSection.guest') }}
      <template #actions>
        <UiButton @click="signIn()">{{ t('common.signIn') }}</UiButton>
      </template>
    </UiState>
  </section>
</template>

<style scoped>
.bookmarks {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.bookmarks__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.bookmarks__tabs {
  display: flex;
  gap: var(--space-1);
  padding: 0 var(--space-3);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.bookmarks__tab {
  display: inline-flex;
  align-items: center;
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border-bottom: var(--size-focus-ring) solid transparent;
  color: var(--color-muted);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.bookmarks__tab:hover {
  color: var(--color-text);
}

.bookmarks__tab[aria-current='page'] {
  border-bottom-color: var(--color-accent);
  color: var(--color-text);
}

.bookmarks__tab:focus-visible {
  outline-offset: calc(-1 * var(--size-focus-ring));
}
</style>
