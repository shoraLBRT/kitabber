<script setup lang="ts">
import { cookieSettingsPath } from './utils/navigation'
import { bookmarksKey, useBookmarks } from './composables/useBookmarks'

const route = useRoute()

// One store of the person's bookmarks for every page (E10-07, ADR-0113): a bookmark put on a post's page shows on its
// feed card back in the feed without another read.
provide(bookmarksKey, useBookmarks())
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
