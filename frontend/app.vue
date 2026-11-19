<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <UiToaster />
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

/* Text for screen readers only. */
.visually-hidden {
  position: absolute;
  width: var(--size-border);
  height: var(--size-border);
  margin: calc(-1 * var(--size-border));
  padding: 0;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
  border: 0;
}
</style>
