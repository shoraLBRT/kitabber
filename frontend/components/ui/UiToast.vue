<script setup lang="ts">
// One toast (FR-UI, E02-04, ADR-0013). Hides itself after its duration; the timer stops while the pointer or the
// keyboard focus is on the toast (WCAG 2.2.1) and starts again when they leave. The close button hides it at once.
import { CircleAlert, CircleCheck, Info, X } from 'lucide-vue-next'
import { onBeforeUnmount, onMounted } from 'vue'
import type { Toast } from '../../composables/useToast'
import { t } from '../../i18n'

const props = defineProps<{ toast: Toast }>()
const emit = defineEmits<{ dismiss: [id: number] }>()

const icons = { info: Info, success: CircleCheck, error: CircleAlert }
let timer: ReturnType<typeof setTimeout> | undefined

function start() {
  stop()
  if (props.toast.duration > 0)
    timer = setTimeout(() => emit('dismiss', props.toast.id), props.toast.duration)
}

function stop() {
  if (timer !== undefined) clearTimeout(timer)
  timer = undefined
}

onMounted(start)
onBeforeUnmount(stop)
</script>

<template>
  <li
    class="ui-toast"
    :class="`ui-toast--${toast.tone}`"
    :data-tone="toast.tone"
    @mouseenter="stop"
    @mouseleave="start"
    @focusin="stop"
    @focusout="start"
  >
    <component :is="icons[toast.tone]" class="ui-toast__icon" aria-hidden="true" />
    <p class="ui-toast__message">{{ toast.message }}</p>
    <button
      type="button"
      class="ui-toast__close"
      :aria-label="t('shell.closeToast')"
      @click="emit('dismiss', toast.id)"
    >
      <X aria-hidden="true" />
    </button>
  </li>
</template>

<style scoped>
.ui-toast {
  display: flex;
  align-items: flex-start;
  gap: var(--space-2);
  padding: var(--space-3);
  border: var(--size-border) solid var(--color-border);
  border-left: var(--space-1) solid var(--color-accent);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  box-shadow: 0 var(--space-2) var(--space-6) var(--color-shadow);
  pointer-events: auto;
}

.ui-toast--success {
  border-left-color: var(--color-success);
}

.ui-toast--error {
  border-left-color: var(--color-danger);
}

.ui-toast__icon {
  flex: none;
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  margin-top: var(--space-1);
  color: var(--color-accent);
}

.ui-toast--success .ui-toast__icon {
  color: var(--color-success);
}

.ui-toast--error .ui-toast__icon {
  color: var(--color-danger);
}

.ui-toast__message {
  flex: 1;
  margin: 0;
}

.ui-toast__close {
  display: inline-flex;
  flex: none;
  padding: var(--space-1);
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-muted);
  cursor: pointer;
}

.ui-toast__close :deep(svg) {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}
</style>
