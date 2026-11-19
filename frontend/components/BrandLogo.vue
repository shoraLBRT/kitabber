<script setup lang="ts">
// The wordmark: the mark plus the product name from brand/brand.json (ADR-0011), coloured by the design tokens
// (ADR-0012). Without `theme` it follows the page theme; with it, it pins the light or dark palette.
import brand from '../../brand/brand.json'

withDefaults(defineProps<{ theme?: 'light' | 'dark'; withName?: boolean }>(), {
  theme: undefined,
  withName: true,
})
</script>

<template>
  <span class="brand-logo" :data-theme="theme">
    <svg
      class="brand-logo__mark"
      :viewBox="brand.mark.viewBox"
      width="32"
      height="32"
      role="img"
      :aria-label="withName ? undefined : brand.name"
      :aria-hidden="withName ? 'true' : undefined"
    >
      <rect class="brand-logo__tile" width="100%" height="100%" :rx="brand.mark.radius" />
      <path
        v-for="d in brand.mark.strokes"
        :key="d"
        class="brand-logo__stroke"
        :d="d"
        fill="none"
        stroke-width="3"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>
    <span v-if="withName" class="brand-logo__name">{{ brand.name }}</span>
  </span>
</template>

<style scoped>
.brand-logo {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text);
  font-weight: var(--font-weight-bold);
  font-size: var(--font-size-xl);
  letter-spacing: var(--letter-spacing-tight);
}

.brand-logo__mark {
  width: var(--size-mark);
  height: var(--size-mark);
}

.brand-logo__tile {
  fill: var(--color-accent);
}

.brand-logo__stroke {
  stroke: var(--color-on-accent);
}
</style>
