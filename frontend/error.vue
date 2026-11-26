<script setup lang="ts">
// The error page (E02-07, FR-UI, docs/ui/README.md, ADR-0057): an address no page takes (404) or an error nobody caught
// (500 and the rest), rendered on the server with that status. It stands in the central column of the usual shell, so
// the menu, the header and the bottom bar keep working; «На главную» leaves it, and after a failure «Повторить» loads
// the same address again. Pages whose content is missing or failed to load draw that in place, with the same codes
// (utils/response-status.ts).
import { computed } from 'vue'
import type { NuxtError } from '#app'
import UiButton from './components/ui/UiButton.vue'
import UiState from './components/ui/UiState.vue'
import { t } from './i18n'

const props = defineProps<{ error: NuxtError }>()

const missing = computed(() => props.error.statusCode === 404)

useHead(() => ({ title: missing.value ? t('errorPage.notFound') : t('errorPage.error') }))

const home = () => clearError({ redirect: '/' })
const retry = () => reloadNuxtApp()
</script>

<template>
  <NuxtLayout>
    <UiState
      v-if="missing"
      tone="not-found"
      heading="h1"
      :title="t('errorPage.notFound')"
      data-testid="not-found-page"
    >
      {{ t('errorPage.notFoundText') }}
      <template #actions>
        <a href="/" @click.prevent="home">{{ t('common.home') }}</a>
      </template>
    </UiState>
    <UiState v-else tone="error" heading="h1" :title="t('errorPage.failed')" data-testid="error-page">
      {{ t('errorPage.failedText') }}
      <template #actions>
        <UiButton variant="secondary" @click="retry">{{ t('common.retry') }}</UiButton>
        <a href="/" @click.prevent="home">{{ t('common.home') }}</a>
      </template>
    </UiState>
  </NuxtLayout>
  <UiToaster />
  <AuthWindowHost />
  <FeedQuickPostSheet />
</template>
