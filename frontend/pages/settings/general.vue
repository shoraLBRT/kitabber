<script setup lang="ts">
// «Основные» of the settings (E04-08, FR-UI, docs/ui/settings.md, ADR-0070) at /settings/general: the colour theme,
// kept in the account so every device of the person shows it. A guest signs in first and comes back here.
import GeneralSettingsForm from '~/components/settings/GeneralSettingsForm.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.general') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/general', { external: true })
</script>

<template>
  <SettingsScreen :title="t('settings.general')" testid="settings-general">
    <GeneralSettingsForm />
  </SettingsScreen>
</template>
