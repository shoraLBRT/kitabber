<script setup lang="ts">
// Where Telegram Login returns (#91, ADR-0115): Telegram puts the signed data into the fragment (#tgAuthResult=),
// which never reaches a server, so this page hands it to the API's callback, which checks the signature and finishes
// the sign-in like any other provider. Without the data (the person declined) the callback answers with the error.
import { telegramCallbackQuery } from '~/api/external-sign-in'
import { t } from '../../i18n'

useHead({ title: t('pages.login') })

const apiBase = useRuntimeConfig().public.apiBase

onMounted(() => {
  const query = telegramCallbackQuery(window.location.hash, window.location.search) ?? 'error=access_denied'
  window.location.replace(`${apiBase}/api/v1/sign-in/external/telegram/callback?${query}`)
})
</script>

<template>
  <p class="telegram-return" role="status">{{ t('auth.external.forwarding') }}</p>
</template>

<style scoped>
.telegram-return {
  margin: 0;
  padding: var(--space-8) var(--space-4);
  color: var(--color-muted);
  text-align: center;
}
</style>
