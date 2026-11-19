<script setup lang="ts">
// Layout of the design showcases (/brand, /tokens, /ui): wide pages without the shell's columns.
</script>

<template>
  <div>
    <header class="bare-header">
      <NuxtLink to="/" class="bare-header__home"><BrandLogo /></NuxtLink>
    </header>
    <slot />
  </div>
</template>

<style scoped>
.bare-header {
  padding: var(--space-3) var(--space-4);
}

.bare-header__home {
  text-decoration: none;
}
</style>
