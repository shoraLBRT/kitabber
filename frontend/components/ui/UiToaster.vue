<script setup lang="ts">
// Toast area (FR-UI, E02-04, ADR-0013), mounted once in app.vue. Two live regions exist from the start so screen
// readers announce new toasts: errors in an assertive one (role="alert"), the rest in a polite one (role="status").
// Bottom-right on the desktop, across the bottom on a phone.
import { computed } from 'vue'
import { useToast } from '../../composables/useToast'
import UiToast from './UiToast.vue'
import { t } from '../../i18n'

const { toasts, dismiss } = useToast()
const errors = computed(() => toasts.filter((toast) => toast.tone === 'error'))
const others = computed(() => toasts.filter((toast) => toast.tone !== 'error'))
</script>

<template>
  <section class="ui-toaster" :aria-label="t('common.notifications')">
    <ol role="status" aria-live="polite" class="ui-toaster__list" data-testid="toasts-polite">
      <UiToast v-for="toast in others" :key="toast.id" :toast="toast" @dismiss="dismiss" />
    </ol>
    <ol role="alert" aria-live="assertive" class="ui-toaster__list" data-testid="toasts-assertive">
      <UiToast v-for="toast in errors" :key="toast.id" :toast="toast" @dismiss="dismiss" />
    </ol>
  </section>
</template>

<style scoped>
.ui-toaster {
  position: fixed;
  z-index: 60;
  right: var(--space-4);
  bottom: var(--space-4);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  width: min(var(--size-aside), calc(100vw - 2 * var(--space-4)));
  pointer-events: none;
}

.ui-toaster__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

/* Below breakpoint.md toasts sit above the bottom bar of the phone shell (docs/ui/README.md, E02-06). */
@media (max-width: 924px) {
  .ui-toaster {
    bottom: calc(
      var(--size-tab-bar) + 2 * var(--space-1) + var(--size-border) + var(--space-2) +
        env(safe-area-inset-bottom)
    );
  }
}

@media (max-width: 639px) {
  .ui-toaster {
    right: var(--space-2);
    left: var(--space-2);
    width: auto;
  }
}
</style>
