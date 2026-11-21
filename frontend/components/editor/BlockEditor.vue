<script setup lang="ts">
// Block editor core (E05-01, ADR-0026): Editor.js behind the EditorEngine interface, the document in the schema
// format of ADR-0023 (v-model:document), and undo/redo across blocks by snapshots (editor/history.ts). Ctrl+Z (Cmd+Z)
// undoes, Ctrl+Shift+Z and Ctrl+Y redo; the keys are read by position, so they work in the Russian layout too.
// States of docs/ui/editor.md: loading (skeleton lines while the engine starts), empty (the placeholder), error
// (the engine failed to start: a message and "Повторить").
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { toEditorData, toPostDocument, type PostDocument } from '../../editor/document'
import { createEditorJs, type CreateEngine, type EditorEngine } from '../../editor/engine'
import { EditorHistory, type Snapshot } from '../../editor/history'

const props = withDefaults(
  defineProps<{
    createEngine?: CreateEngine
    /** Milliseconds now, for the history's typing merge; tests pass their clock. */
    now?: () => number
  }>(),
  { createEngine: createEditorJs, now: undefined },
)

const document = defineModel<PostDocument>('document', { required: true })

const state = ref<'loading' | 'ready' | 'error'>('loading')
const canUndo = ref(false)
const canRedo = ref(false)
// The history position, on the root as data-step: tests wait on it instead of on the timing of Editor.js.
const step = ref(0)
const holder = ref<HTMLElement>()
const root = ref<HTMLElement>()

let engine: EditorEngine | null = null
let history: EditorHistory | null = null
let applying = false
let disposed = false
// Changes, undo and redo run one after another: each reads the editor that the previous one left.
let queue: Promise<void> = Promise.resolve()

function enqueue(task: () => Promise<void>): Promise<void> {
  // A failed step must not stop the ones after it; engine errors after start are not expected (only known tools).
  queue = queue.then(task).catch(() => undefined)
  return queue
}

function syncFlags() {
  canUndo.value = history?.canUndo ?? false
  canRedo.value = history?.canRedo ?? false
  step.value = history?.position ?? 0
}

async function start() {
  state.value = 'loading'
  engine?.destroy()
  engine = null
  if (holder.value) holder.value.replaceChildren()
  try {
    const started = await props.createEngine({
      holder: holder.value!,
      data: toEditorData(document.value),
      placeholder: 'Начните писать…',
      onChange: () => void enqueue(capture),
    })
    if (disposed) {
      started.destroy()
      return
    }
    engine = started
    // The first step is what the engine shows, e.g. the empty paragraph Editor.js puts into an empty document, so
    // undoing to the start renders exactly that and its echo is not a new step.
    const initial = toPostDocument(await started.save())
    history = new EditorHistory({ document: initial, caret: null }, { now: props.now })
    document.value = initial
    syncFlags()
    state.value = 'ready'
  } catch {
    state.value = 'error'
  }
}

/** Reads the editor into the document and records a history step if it changed. */
async function capture() {
  if (!engine || !history || applying) return
  const next = toPostDocument(await engine.save())
  if (history.record({ document: next, caret: engine.caret() })) document.value = next
  syncFlags()
}

async function apply(snapshot: Snapshot) {
  if (!engine) return
  applying = true
  try {
    await engine.render(toEditorData(snapshot.document))
    // Rendering replaces the blocks and drops the focus; without a recorded caret it goes to the first block, so
    // the next Ctrl+Z or Ctrl+Shift+Z still reaches the editor.
    engine.setCaret(snapshot.caret ?? { block: 0, offset: 0 })
    document.value = snapshot.document
  } finally {
    applying = false
  }
}

/** Undoes the last step; a change not yet reported by the engine is recorded first, so it is not lost. */
function undo(): Promise<void> {
  return enqueue(async () => {
    await capture()
    const snapshot = history?.undo()
    if (snapshot) await apply(snapshot)
    syncFlags()
  })
}

function redo(): Promise<void> {
  return enqueue(async () => {
    await capture()
    const snapshot = history?.redo()
    if (snapshot) await apply(snapshot)
    syncFlags()
  })
}

// The shortcuts are read on the document, not only inside the blocks: undo and redo re-render the blocks, and a key
// pressed while that runs reaches the page body. They count when the focus is in the editor's dialog (or in the
// editor), or nowhere in particular (the body).
function onKeydown(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || event.altKey || !root.value) return
  const isUndo = event.code === 'KeyZ' && !event.shiftKey
  const isRedo = (event.code === 'KeyZ' && event.shiftKey) || (event.code === 'KeyY' && !event.shiftKey)
  if (!isUndo && !isRedo) return
  const scope = root.value.closest('[role="dialog"]') ?? root.value
  const target = event.target instanceof Node ? event.target : null
  if (target !== window.document.body && !(target && scope.contains(target))) return
  event.preventDefault()
  event.stopPropagation()
  void (isUndo ? undo() : redo())
}

onMounted(() => {
  window.document.addEventListener('keydown', onKeydown, true)
  void start()
})

onBeforeUnmount(() => {
  window.document.removeEventListener('keydown', onKeydown, true)
  disposed = true
  engine?.destroy()
  engine = null
})

defineExpose({ undo, redo, canUndo, canRedo, state })
</script>

<template>
  <div ref="root" class="block-editor" :data-state="state" :data-step="step" data-testid="block-editor">
    <div v-if="state === 'loading'" class="block-editor__skeleton" aria-hidden="true">
      <span v-for="line in 3" :key="line" class="block-editor__line" />
    </div>
    <p v-if="state === 'loading'" class="block-editor__sr" role="status">Редактор загружается…</p>
    <div v-if="state === 'error'" class="block-editor__error" role="alert">
      <p class="block-editor__error-text">Не удалось открыть редактор. Текст не потерян.</p>
      <button type="button" class="block-editor__retry" @click="start">Повторить</button>
    </div>
    <div
      v-show="state === 'ready'"
      ref="holder"
      class="block-editor__holder"
      data-testid="block-editor-holder"
    />
  </div>
</template>

<style scoped>
.block-editor {
  font-size: var(--font-size-reading);
  line-height: var(--line-height-reading);
}

.block-editor__holder {
  min-height: var(--space-12);
}

.block-editor__skeleton {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding-block: var(--space-2);
}

.block-editor__line {
  height: var(--space-4);
  border-radius: var(--radius-sm);
  background: var(--color-surface-subtle);
}

.block-editor__line:last-child {
  width: 60%;
}

.block-editor__sr {
  position: absolute;
  width: var(--size-border);
  height: var(--size-border);
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

/* The floating panel of the text block (E05-02): placed above the selection by the engine (data-placement); its link
   field opens upwards when it fits there (data-actions), so it does not cover the selected line. */
.block-editor
  :deep(.ce-inline-toolbar[data-actions='top'] .ce-popover--nested-level-1 .ce-popover__container) {
  top: auto;
  bottom: var(--space-1);
}

/* Heading blocks (E05-03): sizes of the reader page (docs/ui/post.md), the placeholder while empty. */
.block-editor :deep(.ce-header) {
  margin: 0;
  padding-block: var(--space-2);
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-tight);
  outline: none;
}

.block-editor :deep(h2.ce-header) {
  font-size: var(--font-size-2xl);
}

.block-editor :deep(h3.ce-header) {
  font-size: var(--font-size-xl);
}

.block-editor :deep(.ce-header:empty::before) {
  content: attr(data-placeholder);
  color: var(--color-muted);
}

/* List blocks (E05-04): items are flat elements with data-level; a nested item is indented one step and numbered on
   its own under its parent (1, 2 and a, b), as on the reader page. */
.block-editor :deep(.cdx-list) {
  margin: 0;
  padding-block: var(--space-1);
  padding-inline-start: var(--space-6);
  counter-reset: list-item-top;
}

.block-editor :deep(.cdx-list__item) {
  padding-block: var(--space-1);
  outline: none;
}

.block-editor :deep(.cdx-list__item[data-level='0']) {
  counter-increment: list-item-top;
  counter-reset: list-item-nested;
}

.block-editor :deep(.cdx-list__item[data-level='1']) {
  margin-inline-start: var(--space-6);
  counter-increment: list-item-nested;
}

.block-editor :deep(.cdx-list--unordered > .cdx-list__item[data-level='1']) {
  list-style-type: circle;
}

.block-editor :deep(.cdx-list--ordered > .cdx-list__item[data-level='0']::marker) {
  content: counter(list-item-top) '. ';
}

.block-editor :deep(.cdx-list--ordered > .cdx-list__item[data-level='1']::marker) {
  content: counter(list-item-nested, lower-alpha) '. ';
}

.block-editor :deep(.cdx-list__item:empty::before) {
  content: attr(data-placeholder);
  color: var(--color-muted);
}

/* Quote block (E05-05): a line on the start side, the caption under the text in a smaller muted type. */
.block-editor :deep(.cdx-quote) {
  margin: 0;
  padding-block: var(--space-2);
  padding-inline-start: var(--space-4);
  border-inline-start: var(--space-1) solid var(--color-border);
}

.block-editor :deep(.cdx-quote__text),
.block-editor :deep(.cdx-quote__caption) {
  outline: none;
}

.block-editor :deep(.cdx-quote__caption) {
  padding-block-start: var(--space-1);
  font-size: var(--font-size-sm);
  color: var(--color-muted);
}

.block-editor :deep(.cdx-quote__text:empty::before),
.block-editor :deep(.cdx-quote__caption:empty::before) {
  content: attr(data-placeholder);
  color: var(--color-muted);
}

/* Delimiter block (E05-05): three dots drawn by styles, the element itself stays empty (delimiter.ts). */
.block-editor :deep(.ce-delimiter) {
  padding-block: var(--space-4);
  text-align: center;
  line-height: var(--line-height-tight);
  cursor: default;
}

.block-editor :deep(.ce-delimiter::before) {
  content: '* * *';
  letter-spacing: var(--space-2);
  color: var(--color-muted);
}

.block-editor :deep(.ce-block mark) {
  background: color-mix(in srgb, var(--color-warning) 30%, transparent);
  color: inherit;
}

.block-editor :deep(.link-tool__error) {
  margin: var(--space-1) 0 0;
  color: var(--color-danger);
  font-size: var(--font-size-xs);
  line-height: var(--line-height-tight);
  white-space: normal;
}

.block-editor__error {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
  color: var(--color-danger);
}

.block-editor__error-text {
  margin: 0;
}

.block-editor__retry {
  min-height: var(--size-touch);
  padding: 0 var(--space-4);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font: inherit;
  cursor: pointer;
}
</style>
