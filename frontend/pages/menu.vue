<script setup lang="ts">
// "Меню" screen (E02-06, docs/ui/README.md): opened from the bottom bar of the phone shell, it replaces the left
// column. On wide screens the left menu shows the same items, so the screen simply repeats them there.
const route = useRoute()
</script>

<template>
  <ShellMenuScreen :current-path="route.path" />
</template>
