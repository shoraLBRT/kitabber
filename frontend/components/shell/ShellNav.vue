<script setup lang="ts">
// Left menu of the desktop shell (E02-05, docs/spec.md section 5): feeds, messages, rating, the topic list with
// "Показать все", service links and the colour theme switch. The current screen is marked with aria-current.
import { isActive, mainNav, newTopicPath, serviceNav, topicsPath, type NavItem } from '../../utils/navigation'
import ShellThemeSwitch from './ShellThemeSwitch.vue'
import { t } from '../../i18n'

withDefaults(
  defineProps<{
    /** Path of the current route. */
    currentPath: string
    /** The menu's topics: the person's subscribed ones or the popular ones (E11-02, ADR-0088); empty while they load. */
    topics?: NavItem[]
  }>(),
  { topics: () => [] },
)
</script>

<template>
  <nav class="shell-nav" :aria-label="t('shell.mainNav')" data-testid="shell-nav">
    <ul class="shell-nav__list">
      <li v-for="item in mainNav" :key="item.id">
        <NuxtLink
          :to="item.to"
          class="shell-nav__link"
          :aria-current="isActive(item, currentPath) ? 'page' : undefined"
          :data-nav="item.id"
        >
          <component :is="item.icon" class="shell-nav__icon" aria-hidden="true" />
          {{ item.label }}
        </NuxtLink>
      </li>
    </ul>

    <section class="shell-nav__section" aria-labelledby="shell-nav-topics">
      <h2 id="shell-nav-topics" class="shell-nav__heading">{{ t('common.topics') }}</h2>
      <ul v-if="topics.length" class="shell-nav__list">
        <li v-for="topic in topics" :key="topic.id">
          <NuxtLink
            :to="topic.to"
            class="shell-nav__link"
            :aria-current="isActive(topic, currentPath) ? 'page' : undefined"
          >
            {{ topic.label }}
          </NuxtLink>
        </li>
      </ul>
      <NuxtLink :to="topicsPath" class="shell-nav__more" data-nav="topics">{{
        t('common.showAll')
      }}</NuxtLink>
      <NuxtLink :to="newTopicPath" class="shell-nav__more" data-nav="new-topic">{{
        t('topicCreate.create')
      }}</NuxtLink>
    </section>

    <ul class="shell-nav__service">
      <li v-for="item in serviceNav" :key="item.id">
        <NuxtLink
          :to="item.to"
          class="shell-nav__service-link"
          :aria-current="isActive(item, currentPath) ? 'page' : undefined"
          :data-nav="item.id"
        >
          {{ item.label }}
        </NuxtLink>
      </li>
    </ul>

    <ShellThemeSwitch />
  </nav>
</template>

<style scoped>
.shell-nav {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

.shell-nav__list,
.shell-nav__service {
  margin: 0;
  padding: 0;
  list-style: none;
}

.shell-nav__link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-md);
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.shell-nav__link:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.shell-nav__link[aria-current='page'] {
  background: var(--color-surface);
  color: var(--color-accent);
}

.shell-nav__icon {
  flex: none;
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.shell-nav__section {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.shell-nav__heading {
  margin: 0;
  padding: 0 var(--space-3);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.shell-nav__more {
  padding: var(--space-2) var(--space-3);
  font-size: var(--font-size-sm);
}

.shell-nav__service {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2) var(--space-4);
  padding: 0 var(--space-3);
  font-size: var(--font-size-sm);
}

.shell-nav__service-link {
  color: var(--color-muted);
  text-decoration: none;
}

.shell-nav__service-link:hover,
.shell-nav__service-link[aria-current='page'] {
  color: var(--color-text);
}

.shell-nav > :deep(.theme-switch) {
  padding: 0 var(--space-3);
}
</style>
