<script setup lang="ts">
// Default layout: the shell (E02-05 desktop, E02-06 phone: bottom bar and floating "Написать" below 925 px).
const route = useRoute()
</script>

<template>
  <ShellLayout :current-path="route.path">
    <slot />
  </ShellLayout>
</template>
