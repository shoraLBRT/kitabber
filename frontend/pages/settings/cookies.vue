<script setup lang="ts">
// The cookie settings (E02-13, FR-UI, docs/ui/settings.md, ADR-0066) at /settings/cookies. Open to guests: the
// choice belongs to the browser, not to an account. The banner leads here with «Настроить», and so does the service
// link «Настройки cookie» of the left menu and of the phone «Меню»; it is also a section of the settings (E04-08).
import CookieSettingsForm from '~/components/settings/CookieSettingsForm.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('cookies.title') })
</script>

<template>
  <SettingsScreen :title="t('cookies.title')">
    <CookieSettingsForm />
  </SettingsScreen>
</template>
