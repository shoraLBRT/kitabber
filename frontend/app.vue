<script setup lang="ts">
import { cookieSettingsPath } from './utils/navigation'

const route = useRoute()
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <UiToaster />
  <AuthWindowHost />
  <FeedQuickPostSheet />
  <!-- The cookie banner (E02-13, ADR-0066); the cookie settings page asks the same question itself. -->
  <ShellCookieBanner v-if="route.path !== cookieSettingsPath" />
</template>
