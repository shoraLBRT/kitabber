<script setup lang="ts">
// Floating "Написать" button of the phone shell (E02-06, docs/spec.md section 5, docs/ui/README.md): a round
// --color-accent button above the bottom bar with a --space-4 gap, shown below breakpoint.sm (640 px). From 640 px
// up "Написать" stays in the header. Keeps clear of the safe areas.
import { PenLine } from 'lucide-vue-next'
import { writePath } from '../../utils/navigation'
</script>

<template>
  <NuxtLink :to="writePath" class="write-button" aria-label="Написать" data-testid="write-button">
    <PenLine class="write-button__icon" aria-hidden="true" />
  </NuxtLink>
</template>

<style scoped>
.write-button {
  position: fixed;
  right: calc(var(--space-4) + env(safe-area-inset-right));
  bottom: calc(var(--size-touch) + 2 * var(--space-1) + var(--size-border) + var(--space-4) +
        env(safe-area-inset-bottom));
  z-index: 11;
  display: none;
  align-items: center;
  justify-content: center;
  width: var(--space-12);
  height: var(--space-12);
  border-radius: var(--radius-full);
  background: var(--color-accent);
  color: var(--color-on-accent);
  box-shadow: 0 var(--space-1) var(--space-3) var(--color-shadow);
}

.write-button:hover {
  background: var(--color-accent-hover);
  color: var(--color-on-accent);
}

.write-button__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

@media (max-width: 639px) {
  .write-button {
    display: inline-flex;
  }
}
</style>
