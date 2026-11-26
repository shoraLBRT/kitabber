<script setup lang="ts">
// The «Аккаунт и вход» settings (FR-AUTH, docs/ui/settings.md) at /settings/account: the email change with a code
// from the new address (E03-06, ADR-0020), the password change (E03-05, ADR-0036) and the devices with «Выйти со всех
// устройств» (E03-09, ADR-0078); the sign-in providers and the account's freezing and deletion come with their own
// issues. The calls go through the site's server (server/routes/bff/email*.ts, password.put.ts, sessions*.ts), which
// holds the access token. A guest signs in first and comes
// back here.
import EmailChangeForm from '~/components/settings/EmailChangeForm.vue'
import PasswordChangeForm from '~/components/settings/PasswordChangeForm.vue'
import SessionList from '~/components/settings/SessionList.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { emailChangeApi } from '~/api/email'
import { passwordChangeApi } from '~/api/password'
import { sessionsApi } from '~/api/sessions'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.account') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/account', { external: true })

const api = passwordChangeApi()
const email = emailChangeApi()
const sessions = sessionsApi()
</script>

<template>
  <SettingsScreen :title="t('settings.account')">
    <EmailChangeForm :api="email" />
    <PasswordChangeForm :api="api" />
    <SessionList :api="sessions" />
  </SettingsScreen>
</template>
