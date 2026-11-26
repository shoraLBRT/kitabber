<script setup lang="ts">
// Editor window (E05-01; docs/spec.md section 5, docs/ui/editor.md): on a desktop an 800×700 px dialog in the
// middle, over the dimmed and blurred feed, with "развернуть на весь экран" and "закрыть"; on a phone (narrower than
// 640 px) a full-screen screen over the bottom bar and the floating "Написать", inside the safe areas. A modal
// dialog with a focus trap; Escape, "✕" and a click on the backdrop close it (the page decides what closing does).
// On a phone the screen takes the box of the visual viewport, so the keyboard does not cover its bottom: the panel of
// the toolbar slot stays right above the keyboard and the footer hides while the keyboard is open (E05-10, ADR-0047).
import { Maximize2, Minimize2, X } from 'lucide-vue-next'
import { computed, onBeforeUnmount, onMounted, ref, useId } from 'vue'
import { phoneQuery, useMediaQuery } from '../../composables/useMediaQuery'
import { useVisualViewport } from '../../composables/useVisualViewport'
import { trapTab } from '../../utils/focus'
import { t } from '../../i18n'

defineProps<{ title: string }>()
const emit = defineEmits<{ close: [] }>()

const expanded = ref(false)
const panel = ref<HTMLElement>()
const titleId = useId()
const isPhone = useMediaQuery(phoneQuery)
const viewport = useVisualViewport()
const keyboard = computed(() => isPhone.value && !!viewport.value?.keyboard)
const box = computed(() =>
  isPhone.value && viewport.value
    ? { top: `${viewport.value.top}px`, height: `${viewport.value.height}px`, bottom: 'auto' }
    : undefined,
)

function onKeydown(event: KeyboardEvent) {
  if (panel.value) trapTab(event, panel.value)
}

// Escape is read on the document before the editor: Editor.js handles it in its blocks and stops it there. While a
// menu of the editor is open, Escape closes that menu, not the window.
function onEscape(event: KeyboardEvent) {
  if (event.key !== 'Escape' || panel.value?.querySelector('.ce-popover--opened')) return
  event.preventDefault()
  emit('close')
}

onMounted(() => {
  document.body.style.overflow = 'hidden'
  document.addEventListener('keydown', onEscape, true)
})

onBeforeUnmount(() => {
  document.body.style.overflow = ''
  document.removeEventListener('keydown', onEscape, true)
})
</script>

<template>
  <Teleport to="#teleports">
    <div
      class="editor-window"
      :class="{ 'editor-window--expanded': expanded, 'editor-window--keyboard': keyboard }"
      :style="box"
      :data-keyboard="keyboard ? 'open' : 'closed'"
      data-testid="editor-window"
    >
      <div class="editor-window__backdrop" data-testid="editor-backdrop" @click="emit('close')" />
      <div
        ref="panel"
        class="editor-window__panel"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        data-testid="editor-panel"
        @keydown="onKeydown"
      >
        <header class="editor-window__header">
          <h1 :id="titleId" class="editor-window__title">{{ title }}</h1>
          <slot name="actions" />
          <button
            type="button"
            class="editor-window__button editor-window__expand"
            :aria-label="expanded ? t('editor.collapse') : t('editor.expand')"
            :aria-pressed="expanded"
            data-testid="editor-expand"
            @click="expanded = !expanded"
          >
            <Minimize2 v-if="expanded" aria-hidden="true" />
            <Maximize2 v-else aria-hidden="true" />
          </button>
          <button
            type="button"
            class="editor-window__button"
            :aria-label="t('editor.close')"
            data-testid="editor-close"
            @click="emit('close')"
          >
            <X aria-hidden="true" />
          </button>
        </header>
        <div class="editor-window__body">
          <div class="editor-window__column"><slot /></div>
        </div>
        <slot name="toolbar" />
        <footer v-if="$slots.footer" class="editor-window__footer"><slot name="footer" /></footer>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.editor-window {
  position: fixed;
  z-index: 50;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.editor-window__backdrop {
  position: absolute;
  inset: 0;
  background: var(--color-overlay);
  backdrop-filter: blur(var(--space-1));
}

.editor-window__panel {
  position: relative;
  display: flex;
  flex-direction: column;
  width: min(var(--size-editor-width), 100%);
  height: min(var(--size-editor-height), 100%);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-text);
  box-shadow: 0 var(--space-4) var(--space-12) var(--color-shadow);
}

.editor-window--expanded .editor-window__panel {
  width: 100%;
  height: 100%;
  border-radius: 0;
}

.editor-window__header {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  padding: var(--space-3) var(--space-3) var(--space-3) var(--space-5);
  border-bottom: var(--size-border) solid var(--color-border);
}

.editor-window__title {
  flex: 1;
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-tight);
}

.editor-window__button,
.editor-window :slotted(.editor-window__button) {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--size-touch);
  height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-muted);
  cursor: pointer;
}

.editor-window__button:hover:not(:disabled),
.editor-window :slotted(.editor-window__button:hover:not(:disabled)) {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.editor-window :slotted(.editor-window__button:disabled) {
  opacity: 0.4;
  cursor: default;
}

.editor-window__button :deep(svg),
.editor-window :slotted(.editor-window__button svg) {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.editor-window__body {
  flex: 1;
  overflow-y: auto;
  padding: var(--space-5);
}

.editor-window__footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-5);
  border-top: var(--size-border) solid var(--color-border);
}

.editor-window__column {
  max-width: var(--size-measure);
  margin: 0 auto;
}

/* Phone (docs/spec.md section 5): the editor is a full-screen screen inside the safe areas. */
@media (max-width: 639px) {
  .editor-window__panel {
    width: 100%;
    height: 100%;
    padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom)
      env(safe-area-inset-left);
    border-radius: 0;
  }

  .editor-window__expand {
    display: none;
  }

  .editor-window__body {
    padding: var(--space-4);
  }

  .editor-window__footer {
    padding: var(--space-2) var(--space-4);
  }

  /* The keyboard is under the screen: the toolbar slot is the bottom row, right above it, and the bottom safe area
     is the keyboard's. */
  .editor-window--keyboard .editor-window__footer {
    display: none;
  }

  .editor-window--keyboard .editor-window__panel {
    padding-bottom: 0;
  }
}
</style>
