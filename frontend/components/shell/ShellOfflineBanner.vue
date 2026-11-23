<script setup lang="ts">
// The «Нет подключения» bar (E02-07, FR-UI, docs/ui/README.md, ADR-0057): above the content of the central column while
// the browser is offline. It is a status, so a screen reader says it once without moving the focus; when the network
// comes back the bar goes and what failed to load is read again (plugins/network.client.ts).
import { WifiOff } from 'lucide-vue-next'
</script>

<template>
  <p class="shell-offline" role="status" data-testid="offline-banner">
    <WifiOff class="shell-offline__icon" aria-hidden="true" />
    <span>Нет подключения. Покажем новое, когда сеть вернётся.</span>
  </p>
</template>

<style scoped>
.shell-offline {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin: 0 0 var(--space-4);
  padding: var(--space-3) var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
  color: var(--color-text);
  font-size: var(--font-size-sm);
}

.shell-offline__icon {
  flex-shrink: 0;
  width: var(--space-5);
  height: var(--space-5);
  color: var(--color-warning);
}

@media (max-width: 639px) {
  .shell-offline {
    border-radius: 0;
  }
}
</style>
