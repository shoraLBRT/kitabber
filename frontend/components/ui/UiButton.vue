<script setup lang="ts">
// Button (E02-04, ADR-0013). A native <button>: Enter and Space work without extra code. While loading it stays
// focusable, announces aria-busy and ignores clicks. An icon-only button needs an aria-label from the caller.
import { LoaderCircle } from 'lucide-vue-next'

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
    size?: 'sm' | 'md'
    type?: 'button' | 'submit' | 'reset'
    disabled?: boolean
    loading?: boolean
    block?: boolean
  }>(),
  { variant: 'primary', size: 'md', type: 'button' },
)

const emit = defineEmits<{ click: [event: MouseEvent] }>()

function onClick(event: MouseEvent) {
  if (props.loading) {
    event.preventDefault()
    return
  }
  emit('click', event)
}
</script>

<template>
  <button
    :type="type"
    class="ui-button"
    :class="[`ui-button--${variant}`, `ui-button--${size}`, { 'ui-button--block': block }]"
    :disabled="disabled"
    :aria-busy="loading ? 'true' : undefined"
    :aria-disabled="loading ? 'true' : undefined"
    @click="onClick"
  >
    <LoaderCircle v-if="loading" class="ui-button__spinner" aria-hidden="true" />
    <slot v-else name="icon" />
    <span v-if="$slots.default" class="ui-button__label"><slot /></span>
  </button>
</template>

<style scoped>
.ui-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: var(--space-10);
  padding: var(--space-2) var(--space-4);
  border: var(--size-border) solid transparent;
  border-radius: var(--radius-md);
  font-family: inherit;
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-medium);
  line-height: var(--line-height-tight);
  cursor: pointer;
}

.ui-button--sm {
  min-height: var(--space-8);
  padding: var(--space-1) var(--space-3);
  font-size: var(--font-size-sm);
}

.ui-button--block {
  width: 100%;
}

.ui-button--primary {
  background: var(--color-accent);
  color: var(--color-on-accent);
}

.ui-button--primary:hover:not(:disabled) {
  background: var(--color-accent-hover);
}

.ui-button--secondary {
  border-color: var(--color-border-strong);
  background: var(--color-surface);
  color: var(--color-text);
}

.ui-button--secondary:hover:not(:disabled),
.ui-button--ghost:hover:not(:disabled) {
  background: var(--color-surface-subtle);
}

.ui-button--ghost {
  background: transparent;
  color: var(--color-text);
}

.ui-button--danger {
  border-color: var(--color-danger);
  background: var(--color-surface);
  color: var(--color-danger);
}

.ui-button--danger:hover:not(:disabled) {
  background: var(--color-surface-subtle);
}

.ui-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ui-button[aria-busy='true'] {
  cursor: progress;
}

.ui-button :deep(svg) {
  flex: none;
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.ui-button__spinner {
  animation: ui-button-spin 0.8s linear infinite;
}

@keyframes ui-button-spin {
  to {
    transform: rotate(1turn);
  }
}

@media (prefers-reduced-motion: reduce) {
  .ui-button__spinner {
    animation: none;
  }
}
</style>
