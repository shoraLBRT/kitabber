<script setup lang="ts">
// One section of the settings as a screen (E04-08, docs/ui/settings.md): its heading and its card. Where the right
// column with the sections is hidden (below 1240 px) the heading starts with «←» back to the list of /settings, as a
// phone opens a section from that list.
import { settingsPath } from '../../utils/navigation'
import { t } from '../../i18n'

defineProps<{ title: string; testid?: string }>()
</script>

<template>
  <section class="settings-screen" aria-labelledby="settings-title" :data-testid="testid">
    <header class="settings-screen__header">
      <NuxtLink
        :to="settingsPath"
        class="settings-screen__back"
        :aria-label="t('settings.backToList')"
        data-testid="settings-list-back"
      >
        ←
      </NuxtLink>
      <h1 id="settings-title" class="settings-screen__title">{{ title }}</h1>
    </header>
    <div class="settings-screen__card">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.settings-screen {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.settings-screen__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.settings-screen__back {
  display: none;
  align-items: center;
  justify-content: center;
  min-width: var(--size-touch);
  min-height: var(--size-touch);
  color: var(--color-text);
  font-size: var(--font-size-xl);
  text-decoration: none;
}

.settings-screen__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

.settings-screen__card {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

@media (max-width: 1239px) {
  .settings-screen__back {
    display: inline-flex;
  }
}

@media (max-width: 639px) {
  .settings-screen__header {
    padding: 0 var(--space-2);
  }

  .settings-screen__card {
    padding: var(--space-4);
    border-radius: 0;
  }
}
</style>
