<script setup lang="ts">
// Modal dialog (E02-04, ADR-0013), WAI-ARIA "dialog (modal)" pattern. On open, focus moves inside
// ([data-autofocus], else the first focusable element of the content, else the close button); Tab and Shift+Tab
// stay inside; Escape, the close button and a click on the backdrop close it; on close focus returns to where it
// was. The page behind does not scroll. On a phone (narrower than 640 px, docs/spec.md section 5) the same
// dialog is shown as a bottom sheet; presentation="sheet" (UiSheet) shows it as a sheet at every width. `fullscreen`
// makes that sheet fill the screen (the sign-in window on a phone, E03-04); the modal keeps its size.
import { X } from 'lucide-vue-next'
import { computed, nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { phoneQuery, useMediaQuery } from '../../composables/useMediaQuery'
import { focusableIn, trapTab } from '../../utils/focus'

const props = withDefaults(
  defineProps<{
    title: string
    presentation?: 'auto' | 'modal' | 'sheet'
    closeLabel?: string
    fullscreen?: boolean
  }>(),
  { presentation: 'auto', closeLabel: 'Закрыть' },
)

const open = defineModel<boolean>('open', { default: false })

const isPhone = useMediaQuery(phoneQuery)
const mode = computed(() =>
  props.presentation === 'auto' ? (isPhone.value ? 'sheet' : 'modal') : props.presentation,
)

const panel = ref<HTMLElement>()
const content = ref<HTMLElement>()
const closeButton = ref<HTMLButtonElement>()
const titleId = useId()
let returnTo: HTMLElement | null = null
let locked = false

function close() {
  open.value = false
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    close()
  } else if (panel.value) {
    trapTab(event, panel.value)
  }
}

function setLocked(value: boolean) {
  if (value === locked) return
  locked = value
  lockCount += value ? 1 : -1
  document.body.style.overflow = lockCount > 0 ? 'hidden' : ''
}

watch(
  open,
  async (isOpen) => {
    if (typeof document === 'undefined') return
    if (isOpen) {
      returnTo = document.activeElement instanceof HTMLElement ? document.activeElement : null
      setLocked(true)
      await nextTick()
      const target =
        panel.value?.querySelector<HTMLElement>('[data-autofocus]') ??
        (content.value ? focusableIn(content.value)[0] : undefined) ??
        closeButton.value
      target?.focus()
    } else {
      setLocked(false)
      if (returnTo?.isConnected) returnTo.focus()
      returnTo = null
    }
  },
  { immediate: true },
)

onBeforeUnmount(() => setLocked(false))
</script>

<script lang="ts">
// Open dialogs across the page: the body scrolls again only when the last one closes.
let lockCount = 0
</script>

<template>
  <Teleport to="#teleports">
    <div
      v-if="open"
      class="ui-dialog"
      :class="[`ui-dialog--${mode}`, { 'ui-dialog--fullscreen': fullscreen && mode === 'sheet' }]"
      :data-presentation="mode"
    >
      <div class="ui-dialog__backdrop" data-testid="dialog-backdrop" @click="close" />
      <div
        ref="panel"
        class="ui-dialog__panel"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        tabindex="-1"
        @keydown="onKeydown"
      >
        <span v-if="mode === 'sheet' && !fullscreen" class="ui-dialog__handle" aria-hidden="true" />
        <header class="ui-dialog__header">
          <h2 :id="titleId" class="ui-dialog__title">{{ title }}</h2>
          <button
            ref="closeButton"
            type="button"
            class="ui-dialog__close"
            :aria-label="closeLabel"
            @click="close"
          >
            <X aria-hidden="true" />
          </button>
        </header>
        <div ref="content" class="ui-dialog__content"><slot /></div>
        <footer v-if="$slots.footer" class="ui-dialog__footer"><slot name="footer" /></footer>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.ui-dialog {
  position: fixed;
  z-index: 50;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-4);
}

.ui-dialog__backdrop {
  position: absolute;
  inset: 0;
  background: var(--color-overlay);
}

.ui-dialog__panel {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 100%;
  max-width: var(--size-measure);
  max-height: 100%;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-text);
  box-shadow: 0 var(--space-4) var(--space-12) var(--color-shadow);
  animation: ui-dialog-in 0.15s ease-out;
}

.ui-dialog__panel:focus-visible {
  outline: 0;
}

.ui-dialog--sheet {
  align-items: flex-end;
  padding: 0;
}

.ui-dialog--sheet .ui-dialog__panel {
  max-width: none;
  max-height: 90vh;
  border-radius: var(--radius-lg) var(--radius-lg) 0 0;
  animation-name: ui-sheet-in;
}

.ui-dialog--fullscreen .ui-dialog__panel {
  height: 100%;
  max-height: none;
  box-sizing: border-box;
  padding-top: env(safe-area-inset-top);
  border-radius: 0;
}

.ui-dialog__handle {
  align-self: center;
  width: var(--space-10);
  height: var(--space-1);
  margin-top: var(--space-2);
  border-radius: var(--radius-full);
  background: var(--color-border-strong);
}

.ui-dialog__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-4) var(--space-4) var(--space-2) var(--space-5);
}

.ui-dialog__title {
  flex: 1;
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-tight);
}

.ui-dialog__close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--space-10);
  height: var(--space-10);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-muted);
  cursor: pointer;
}

.ui-dialog__close:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.ui-dialog__close :deep(svg) {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.ui-dialog__content {
  overflow-y: auto;
  padding: var(--space-2) var(--space-5) var(--space-5);
}

.ui-dialog__footer {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-5) var(--space-5);
  border-top: var(--size-border) solid var(--color-border);
}

.ui-dialog--sheet .ui-dialog__footer {
  padding-bottom: calc(var(--space-5) + env(safe-area-inset-bottom));
}

@keyframes ui-dialog-in {
  from {
    opacity: 0;
    transform: scale(0.97);
  }
}

@keyframes ui-sheet-in {
  from {
    transform: translateY(100%);
  }
}

@media (prefers-reduced-motion: reduce) {
  .ui-dialog__panel {
    animation: none;
  }
}
</style>
