<script setup lang="ts">
// The wordmark: the mark plus the product name, both from brand/brand.json (ADR-0011).
import brand from '../../../brand/brand.json'

withDefaults(defineProps<{ theme?: 'light' | 'dark'; withName?: boolean }>(), {
  theme: 'light',
  withName: true,
})
</script>

<template>
  <span class="brand-logo" :style="{ color: brand.palette[theme].text }">
    <svg
      class="brand-logo__mark"
      :viewBox="brand.mark.viewBox"
      width="32"
      height="32"
      role="img"
      :aria-label="withName ? undefined : brand.name"
      :aria-hidden="withName ? 'true' : undefined"
    >
      <rect width="100%" height="100%" :rx="brand.mark.radius" :fill="brand.palette[theme].accent" />
      <path
        v-for="d in brand.mark.strokes"
        :key="d"
        :d="d"
        fill="none"
        :stroke="brand.palette[theme].onAccent"
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
  gap: 0.5rem;
  font-weight: 700;
  font-size: 1.375rem;
  letter-spacing: -0.02em;
}
</style>
