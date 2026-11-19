<template>
  <div>
    <header class="app-header">
      <NuxtLink to="/" class="app-header__home"><BrandLogo /></NuxtLink>
    </header>
    <NuxtPage />
    <UiToaster />
  </div>
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

a {
  color: var(--color-accent);
}

a:hover {
  color: var(--color-accent-hover);
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

.app-header__home {
  text-decoration: none;
}
</style>
