<script setup lang="ts">
// The settings (E04-08, docs/ui/settings.md, ADR-0070) at /settings: the list of sections. A phone opens it from the
// «Меню», and each section then opens as a screen of its own at its address. Open to guests too — «Настройки cookie»
// is theirs; a section that belongs to an account sends a guest to sign in.
import SettingsNav from '~/components/settings/SettingsNav.vue'
import { t } from '../../i18n'

useHead({ title: t('settings.title') })
const route = useRoute()
</script>

<template>
  <section class="settings-list" aria-labelledby="settings-title" data-testid="settings-list">
    <h1 id="settings-title" class="settings-list__title">{{ t('settings.title') }}</h1>
    <SettingsNav :current-path="route.path" variant="list" />
  </section>
</template>

<style scoped>
.settings-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.settings-list__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

@media (max-width: 639px) {
  .settings-list__title {
    padding: 0 var(--space-4);
  }
}
</style>
