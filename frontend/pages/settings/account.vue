<script setup lang="ts">
// The «Аккаунт и вход» settings (FR-AUTH, docs/ui/settings.md) at /settings/account. For now only the password
// change (E03-05, ADR-0036); the email, the sign-in providers, the devices and the account's freezing and deletion
// come with their own issues. The change goes through the site's server (server/routes/bff/password.put.ts), which
// holds the access token. A guest signs in first and comes back here.
import PasswordChangeForm from '~/components/settings/PasswordChangeForm.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { passwordChangeApi } from '~/api/password'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.account') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/account', { external: true })

const api = passwordChangeApi()
</script>

<template>
  <SettingsScreen :title="t('settings.account')">
    <PasswordChangeForm :api="api" />
  </SettingsScreen>
</template>
