<script setup lang="ts">
// The «Аккаунт и вход» settings (FR-AUTH, docs/ui/settings.md) at /settings/account. For now only the password
// change (E03-05, ADR-0036); the email, the sign-in providers, the devices and the account's freezing and deletion
// come with their own issues. The change goes through the site's server (server/routes/bff/password.put.ts), which
// holds the access token. A guest signs in first and comes back here.
import PasswordChangeForm from '~/components/settings/PasswordChangeForm.vue'
import { passwordChangeApi } from '~/api/password'
import { t } from '../../i18n'

useHead({ title: t('settings.account') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/account', { external: true })

const api = passwordChangeApi()
</script>

<template>
  <section class="settings" aria-labelledby="settings-title">
    <h1 id="settings-title" class="settings__title">{{ t('settings.account') }}</h1>
    <div class="settings__card">
      <PasswordChangeForm :api="api" />
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
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
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
