<script setup lang="ts">
// The panel above the keyboard of a phone (E05-10, docs/ui/editor.md «Телефон, 390 px», ADR-0047): «+» opens the
// block menu (a bottom sheet, E05-07), «Ж» and «К» make the selection bold or italic. It lives in the toolbar slot of
// EditorWindow, which keeps the slot right above the keyboard. Its buttons do not take the focus (the press is
// cancelled, and BlockEditor puts back the selection kept when it began), so the caret and the keyboard stay in the
// text while a tool is used. Wider than a phone
// the panel is not shown: there the block toolbar of Editor.js and the floating panel do the same.
// The photo from the gallery and the camera add a «Фото» block (E05-06); publish settings come with E05-24.
import { Bold, Camera, Image as ImageIcon, Italic, Plus } from 'lucide-vue-next'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { phoneQuery, useMediaQuery } from '../../composables/useMediaQuery'
import type { InlineMark } from '../../editor/engine'
import { t } from '../../i18n'

/** What the panel needs from BlockEditor (its exposed methods). */
export interface KeyboardBarTarget {
  readonly state: 'loading' | 'ready' | 'error'
  keepSelection(): void
  openBlockMenu(): void
  format(mark: InlineMark): void
  addImage(file: File): void
}

const props = defineProps<{ editor: KeyboardBarTarget | undefined }>()

const isPhone = useMediaQuery(phoneQuery)
const pressed = ref<Record<InlineMark, boolean>>({ bold: false, italic: false })

const marks: { mark: InlineMark; label: string; icon: typeof Bold }[] = [
  { mark: 'bold', label: t('editor.bold'), icon: Bold },
  { mark: 'italic', label: t('editor.italic'), icon: Italic },
]

// Whether the selection is already bold or italic, for aria-pressed; only while the panel is shown, jsdom has none.
function readPressed() {
  if (!isPhone.value || typeof document.queryCommandState !== 'function') return
  pressed.value = { bold: document.queryCommandState('bold'), italic: document.queryCommandState('italic') }
}

function onPress(event: PointerEvent) {
  props.editor?.keepSelection()
  event.preventDefault()
}

// The gallery and the camera (ADR-0047 point 5): the same file input, the camera's with capture. The picked picture
// becomes a «Фото» block after the caret's block (E05-06).
const sources = [
  { id: 'gallery', label: t('editor.gallery'), icon: ImageIcon },
  { id: 'camera', label: t('editor.camera'), icon: Camera },
] as const
const gallery = ref<HTMLInputElement>()
const camera = ref<HTMLInputElement>()

function pick(source: 'gallery' | 'camera') {
  ;(source === 'gallery' ? gallery : camera).value?.click()
}

function onPicked(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) props.editor?.addImage(file)
}

function format(mark: InlineMark) {
  props.editor?.format(mark)
  readPressed()
}

onMounted(() => document.addEventListener('selectionchange', readPressed))
onBeforeUnmount(() => document.removeEventListener('selectionchange', readPressed))
</script>

<template>
  <div
    v-if="isPhone"
    class="keyboard-bar"
    role="toolbar"
    :aria-label="t('editor.tools')"
    data-testid="editor-keyboard-bar"
    @pointerdown="onPress"
    @mousedown.prevent
  >
    <button
      type="button"
      class="keyboard-bar__button"
      :aria-label="t('editor.addBlock')"
      data-testid="keyboard-bar-add"
      :disabled="editor?.state !== 'ready'"
      @click="editor?.openBlockMenu()"
    >
      <Plus aria-hidden="true" />
    </button>
    <button
      v-for="source in sources"
      :key="source.id"
      type="button"
      class="keyboard-bar__button"
      :aria-label="source.label"
      :data-testid="`keyboard-bar-${source.id}`"
      :disabled="editor?.state !== 'ready'"
      @click="pick(source.id)"
    >
      <component :is="source.icon" aria-hidden="true" />
    </button>
    <input
      ref="gallery"
      type="file"
      accept="image/*"
      hidden
      data-testid="keyboard-bar-gallery-input"
      @change="onPicked"
    />
    <input
      ref="camera"
      type="file"
      accept="image/*"
      capture="environment"
      hidden
      data-testid="keyboard-bar-camera-input"
      @change="onPicked"
    />
    <span class="keyboard-bar__divider" aria-hidden="true" />
    <button
      v-for="item in marks"
      :key="item.mark"
      type="button"
      class="keyboard-bar__button"
      :aria-label="item.label"
      :aria-pressed="pressed[item.mark]"
      :data-testid="`keyboard-bar-${item.mark}`"
      :disabled="editor?.state !== 'ready'"
      @click="format(item.mark)"
    >
      <component :is="item.icon" aria-hidden="true" />
    </button>
  </div>
</template>

<style scoped>
.keyboard-bar {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  gap: var(--space-1);
  padding: var(--space-1) calc(var(--space-2) + env(safe-area-inset-right)) var(--space-1)
    calc(var(--space-2) + env(safe-area-inset-left));
  overflow-x: auto;
  border-top: var(--size-border) solid var(--color-border);
  background: var(--color-surface);
  touch-action: manipulation;
}

.keyboard-bar__button {
  display: inline-flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: var(--size-touch);
  height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
}

.keyboard-bar__button[aria-pressed='true'] {
  background: var(--color-surface-subtle);
  color: var(--color-accent);
}

.keyboard-bar__button:disabled {
  opacity: 0.4;
  cursor: default;
}

.keyboard-bar__button :deep(svg) {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.keyboard-bar__divider {
  width: var(--size-border);
  height: var(--space-6);
  background: var(--color-border);
}
</style>
