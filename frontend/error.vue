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

const props = defineProps<{ error: NuxtError }>()

const missing = computed(() => props.error.statusCode === 404)

useHead(() => ({ title: missing.value ? 'Страница не найдена' : 'Ошибка' }))

const home = () => clearError({ redirect: '/' })
const retry = () => reloadNuxtApp()
</script>

<template>
  <NuxtLayout>
    <UiState
      v-if="missing"
      tone="not-found"
      heading="h1"
      title="Страница не найдена"
      data-testid="not-found-page"
    >
      Возможно, адрес набран с ошибкой или страница удалена.
      <template #actions>
        <a href="/" @click.prevent="home">На главную</a>
      </template>
    </UiState>
    <UiState v-else tone="error" heading="h1" title="Что-то пошло не так" data-testid="error-page">
      Сайт не смог показать эту страницу. Попробуйте ещё раз чуть позже.
      <template #actions>
        <UiButton variant="secondary" @click="retry">Повторить</UiButton>
        <a href="/" @click.prevent="home">На главную</a>
      </template>
    </UiState>
  </NuxtLayout>
  <UiToaster />
  <AuthWindowHost />
  <FeedQuickPostSheet />
</template>
