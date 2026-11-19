<script setup lang="ts">
import BrandLogo from '@/components/BrandLogo.vue'
</script>

<template>
  <header class="app-header"><BrandLogo /></header>
  <main />
</template>

<style>
/* Page base from the design tokens (brand/tokens.css, ADR-0012). */
body {
  margin: 0;
  background: var(--color-background);
  color: var(--color-text);
  font-family: var(--font-family-sans);
  font-size: var(--font-size-md);
  line-height: var(--line-height-normal);
}

:focus-visible {
  outline: var(--size-focus-ring) solid var(--color-focus);
  outline-offset: var(--size-focus-ring);
}
</style>

<style scoped>
.app-header {
  padding: var(--space-3) var(--space-4);
}
</style>
