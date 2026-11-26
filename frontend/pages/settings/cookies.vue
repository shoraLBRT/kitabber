<script setup lang="ts">
// The cookie settings (E02-13, FR-UI, docs/ui/settings.md, ADR-0066) at /settings/cookies. Open to guests: the
// choice belongs to the browser, not to an account. The banner leads here with «Настроить», and so does the service
// link «Настройки cookie» of the left menu and of the phone «Меню».
import CookieSettingsForm from '~/components/settings/CookieSettingsForm.vue'
import { t } from '../../i18n'

useHead({ title: t('cookies.title') })
</script>

<template>
  <section class="settings" aria-labelledby="settings-title">
    <h1 id="settings-title" class="settings__title">{{ t('cookies.title') }}</h1>
    <div class="settings__card">
      <CookieSettingsForm />
    </div>
  </section>
</template>

<style scoped>
.settings {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.settings__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

.settings__card {
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

@media (max-width: 639px) {
  .settings__card {
    padding: var(--space-4);
  }
}
</style>
