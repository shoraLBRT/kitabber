<script setup lang="ts">
// The sections of the settings (E04-08, docs/ui/settings.md, ADR-0070). `aside` is the desktop's right column, next
// to the open section; `list` is the first screen of /settings, which a phone opens from the «Меню»: rows of at least
// 44 px, each opening its section as a screen of its own.
import { ChevronRight } from 'lucide-vue-next'
import { isActive, settingsNav } from '../../utils/navigation'
import { t } from '../../i18n'

withDefaults(defineProps<{ currentPath: string; variant?: 'aside' | 'list' }>(), { variant: 'aside' })
</script>

<template>
  <nav
    class="settings-nav"
    :class="`settings-nav--${variant}`"
    :aria-label="t('settings.sections')"
    :data-testid="`settings-nav-${variant}`"
  >
    <h2 v-if="variant === 'aside'" class="settings-nav__heading">{{ t('settings.title') }}</h2>
    <ul class="settings-nav__list">
      <li v-for="item in settingsNav" :key="item.id">
        <NuxtLink
          :to="item.to"
          class="settings-nav__row"
          :aria-current="isActive(item, currentPath) ? 'page' : undefined"
          :data-section="item.id"
        >
          <span class="settings-nav__label">{{ item.label }}</span>
          <ChevronRight v-if="variant === 'list'" class="settings-nav__chevron" aria-hidden="true" />
        </NuxtLink>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.settings-nav {
  padding: var(--space-2) 0;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.settings-nav__heading {
  margin: 0;
  padding: var(--space-2) var(--space-4);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.settings-nav__list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.settings-nav__row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-height: var(--size-touch);
  box-sizing: border-box;
  padding: var(--space-2) var(--space-4);
  color: var(--color-text);
  text-decoration: none;
}

.settings-nav__row:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.settings-nav__row[aria-current='page'] {
  color: var(--color-accent);
  font-weight: var(--font-weight-medium);
}

.settings-nav__label {
  flex: 1;
  min-width: 0;
}

.settings-nav__chevron {
  flex-shrink: 0;
  color: var(--color-muted);
}

@media (max-width: 639px) {
  .settings-nav--list {
    border-radius: 0;
  }
}
</style>
