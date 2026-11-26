<script setup lang="ts">
// "Меню" screen of the phone shell (E02-06, docs/spec.md section 5, docs/ui/README.md). It replaces the left
// column below breakpoint.md: profile or "Войти", drafts, bookmarks, settings, topics with "Показать все", the
// colour theme switch and the service links. "Коллекция", "подписка" and donations are not shown.
import { ChevronRight, LogIn, LogOut } from 'lucide-vue-next'
import { computed } from 'vue'
import {
  accountNav,
  isActive,
  loginPath,
  newTopicPath,
  serviceNav,
  topicsPath,
  type NavItem,
} from '../../utils/navigation'
import UiAvatar from '../ui/UiAvatar.vue'
import type { ShellUser } from './ShellHeader.vue'
import ShellThemeSwitch from './ShellThemeSwitch.vue'
import { t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    currentPath: string
    user?: ShellUser | null
    /** Topics of the reader; empty until the topics module exists (FR-TOPIC). */
    topics?: NavItem[]
  }>(),
  { user: null, topics: () => [] },
)
const emit = defineEmits<{ logout: [] }>()

const profile = accountNav.find((item) => item.id === 'profile')!
// Drafts, bookmarks and settings need an account; a guest sees "Войти" instead.
const accountLinks = computed(() => (props.user ? accountNav.filter((item) => item.id !== 'profile') : []))
</script>

<template>
  <section class="menu-screen" aria-labelledby="menu-screen-title" data-testid="menu-screen">
    <h1 id="menu-screen-title" class="menu-screen__title">{{ t('common.menu') }}</h1>

    <div class="menu-screen__card">
      <NuxtLink v-if="user" :to="profile.to" class="menu-screen__profile" data-nav="profile">
        <UiAvatar :name="user.name" :src="user.avatar" size="md" decorative />
        <span class="menu-screen__profile-text">
          <span class="menu-screen__name">{{ user.name }}</span>
          <span class="menu-screen__hint">{{ t('common.profile') }}</span>
        </span>
        <ChevronRight class="menu-screen__chevron" aria-hidden="true" />
      </NuxtLink>
      <NuxtLink v-else :to="loginPath" external class="menu-screen__row" data-nav="login">
        <LogIn class="menu-screen__icon" aria-hidden="true" />
        {{ t('common.signIn') }}
      </NuxtLink>

      <ul v-if="accountLinks.length" class="menu-screen__list">
        <li v-for="item in accountLinks" :key="item.id">
          <NuxtLink
            :to="item.to"
            class="menu-screen__row"
            :aria-current="isActive(item, currentPath) ? 'page' : undefined"
            :data-nav="item.id"
          >
            <component :is="item.icon" class="menu-screen__icon" aria-hidden="true" />
            {{ item.label }}
          </NuxtLink>
        </li>
      </ul>
    </div>

    <section class="menu-screen__card" aria-labelledby="menu-screen-topics">
      <h2 id="menu-screen-topics" class="menu-screen__heading">{{ t('common.topics') }}</h2>
      <ul v-if="topics.length" class="menu-screen__list">
        <li v-for="topic in topics" :key="topic.id">
          <NuxtLink
            :to="topic.to"
            class="menu-screen__row"
            :aria-current="isActive(topic, currentPath) ? 'page' : undefined"
          >
            {{ topic.label }}
          </NuxtLink>
        </li>
      </ul>
      <NuxtLink :to="topicsPath" class="menu-screen__row" data-nav="topics">{{
        t('common.showAll')
      }}</NuxtLink>
      <NuxtLink :to="newTopicPath" class="menu-screen__row" data-nav="new-topic">{{
        t('topicCreate.create')
      }}</NuxtLink>
    </section>

    <div class="menu-screen__card">
      <ShellThemeSwitch />
    </div>

    <div class="menu-screen__card">
      <ul class="menu-screen__list">
        <li v-for="item in serviceNav" :key="item.id">
          <NuxtLink
            :to="item.to"
            class="menu-screen__row"
            :aria-current="isActive(item, currentPath) ? 'page' : undefined"
            :data-nav="item.id"
          >
            {{ item.label }}
          </NuxtLink>
        </li>
      </ul>
      <button v-if="user" type="button" class="menu-screen__row menu-screen__logout" @click="emit('logout')">
        <LogOut class="menu-screen__icon" aria-hidden="true" />
        {{ t('common.signOut') }}
      </button>
    </div>
  </section>
</template>

<style scoped>
.menu-screen {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.menu-screen__title {
  margin: 0;
  padding: 0 var(--space-4);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.menu-screen__card {
  padding: var(--space-2) 0;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.menu-screen__heading {
  margin: 0;
  padding: var(--space-2) var(--space-4);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.menu-screen__list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.menu-screen__row,
.menu-screen__profile {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  width: 100%;
  min-height: var(--size-touch);
  box-sizing: border-box;
  padding: var(--space-2) var(--space-4);
  border: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
  text-align: left;
  text-decoration: none;
  cursor: pointer;
}

.menu-screen__row:hover,
.menu-screen__profile:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.menu-screen__row[aria-current='page'] {
  color: var(--color-accent);
  font-weight: var(--font-weight-medium);
}

.menu-screen__logout {
  color: var(--color-danger);
}

.menu-screen__profile-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.menu-screen__name {
  font-weight: var(--font-weight-medium);
}

.menu-screen__hint {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.menu-screen__icon,
.menu-screen__chevron {
  flex: none;
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.menu-screen__chevron {
  margin-left: auto;
  color: var(--color-muted);
}

.menu-screen :deep(.theme-switch) {
  padding: 0 var(--space-4);
}

@media (max-width: 639px) {
  .menu-screen__card {
    border-radius: 0;
  }
}
</style>
