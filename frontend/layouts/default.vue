<script setup lang="ts">
// Default layout: the desktop shell (E02-05). The phone shell (#16) adds its bottom bar to the "mobile" slot.
const route = useRoute()
</script>

<template>
  <ShellLayout :current-path="route.path">
    <slot />
  </ShellLayout>
</template>
