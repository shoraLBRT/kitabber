<script setup lang="ts">
// «Уведомления» of the settings (E04-08, FR-NOTIFY, docs/ui/settings.md) at /settings/notifications. Only the
// section's place for now: the table «тип события × канал» comes with E12-05 (#202), together with the notifications
// it switches. A guest signs in first and comes back here.
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.notifications') })

const account = useAccount()
if (!account.value.signedIn)
  await navigateTo('/auth/sign-in?returnTo=/settings/notifications', { external: true })
</script>

<template>
  <SettingsScreen :title="t('settings.notifications')" testid="settings-notifications">
    <p class="settings-coming">{{ t('settings.notificationsComing') }}</p>
  </SettingsScreen>
</template>

<style scoped>
.settings-coming {
  margin: 0;
  color: var(--color-muted);
}
</style>
