// The editing engine behind BlockEditor (E05-01, ADR-0026): Editor.js in the app, a fake in component tests. The
// component speaks the schema format (document.ts) and asks the engine only for what undo, redo and the caret need.
import type { OutputData, ToolConstructable } from '@editorjs/editorjs'
import { caretOffsetIn, placeCaret } from './caret'
import type { Caret } from './history'
import { HeaderTool } from './header'
import { cleanPastedHtml } from './inline'
import { LinkTool, MarkerTool } from './tools'

export interface EngineOptions {
  holder: HTMLElement
  data: OutputData
  placeholder: string
  /** Called after the content changed (typing, a block added or removed). */
  onChange: () => void
}

export interface EditorEngine {
  save(): Promise<OutputData>
  render(data: OutputData): Promise<void>
  /** The block with the caret and the text offset in it, or null when the caret is outside the editor. */
  caret(): Caret | null
  setCaret(caret: Caret): void
  destroy(): void
}

export type CreateEngine = (options: EngineOptions) => Promise<EditorEngine>

// Russian labels of the Editor.js interface (docs: interface texts are in Russian). Tools of later tasks add theirs.
const messages = {
  ui: {
    blockTunes: { toggler: { 'Click to tune': 'Настроить', 'or drag to move': 'или перетащите' } },
    inlineToolbar: { converter: { 'Convert to': 'Превратить в' } },
    toolbar: { toolbox: { Add: 'Добавить блок' } },
    popover: { Filter: 'Поиск', 'Nothing found': 'Ничего не найдено', 'Convert to': 'Превратить в' },
  },
  toolNames: {
    Text: 'Текст',
    Heading: 'Заголовок',
    Subheading: 'Подзаголовок',
    Bold: 'Жирный',
    Italic: 'Курсив',
    Marker: 'Маркер',
    Link: 'Ссылка',
  },
  blockTunes: {
    delete: { Delete: 'Удалить', 'Click to delete': 'Нажмите, чтобы удалить' },
    moveUp: { 'Move up': 'Выше' },
    moveDown: { 'Move down': 'Ниже' },
  },
}

/** Editor.js, loaded on demand: it touches the DOM, so it never runs on the server. */
export const createEditorJs: CreateEngine = async ({ holder, data, placeholder, onChange }) => {
  const { default: EditorJS } = await import('@editorjs/editorjs')
  const editor = new EditorJS({
    holder,
    data,
    placeholder,
    autofocus: true,
    minHeight: 0,
    i18n: { messages },
    tools: {
      // An empty paragraph is kept, so block indexes of the saved document match the editor's (undo, caret).
      paragraph: { inlineToolbar: ['bold', 'italic', 'marker', 'link'], config: { preserveBlank: true } },
      // «Заголовок» (H2) and «Подзаголовок» (H3), E05-03: one tool with two entries in the block menu. The same inline
      // tools as the text block: Editor.js cleans converted text by the target's tools, so text↔heading keeps markup.
      header: {
        class: HeaderTool as unknown as ToolConstructable,
        inlineToolbar: ['bold', 'italic', 'marker', 'link'],
      },
      marker: { class: MarkerTool as unknown as ToolConstructable, shortcut: 'CMD+SHIFT+M' },
      // The same name as the built-in link tool, so it takes its place.
      link: { class: LinkTool as unknown as ToolConstructable },
    },
    onChange: () => onChange(),
  })
  await editor.isReady
  const stopCleaning = cleanTransfers(holder)
  const stopPlacing = placeToolbarAbove(holder)

  const inputOf = (index: number): HTMLElement | null => {
    const block = editor.blocks.getBlockByIndex(index)
    return block?.holder.querySelector<HTMLElement>('[contenteditable="true"]') ?? null
  }

  return {
    save: () => editor.save(),
    render: (next) => editor.render(next),
    caret() {
      const block = editor.blocks.getCurrentBlockIndex()
      if (block < 0) return null
      const input = inputOf(block)
      const offset = input ? caretOffsetIn(input, window.getSelection()) : null
      return { block, offset: offset ?? 0 }
    },
    setCaret({ block, offset }) {
      const count = editor.blocks.getBlocksCount()
      if (count === 0) return
      const index = Math.min(Math.max(block, 0), count - 1)
      // setToBlock makes it the current block of Editor.js; placeCaret then sets the exact offset across markup.
      editor.caret.setToBlock(index, 'start')
      const input = inputOf(index)
      if (input) placeCaret(input, offset)
    },
    destroy() {
      stopCleaning()
      stopPlacing()
      editor.destroy()
    },
  }
}

/**
 * Pasted or dropped HTML is cleaned before Editor.js reads it (E05-02): Editor.js splits it into paragraphs and keeps
 * the tags of its tools, but loses the formatting Word and Google Docs give by styles and keeps the text of unknown
 * elements. The text/html of the clipboard is replaced by cleanPastedHtml for this event only.
 */
function cleanTransfers(holder: HTMLElement): () => void {
  const clean = (event: Event) => {
    const data =
      event instanceof ClipboardEvent
        ? event.clipboardData
        : event instanceof DragEvent
          ? event.dataTransfer
          : null
    if (!data) return
    const html = data.getData('text/html')
    if (!html.trim()) return
    const cleaned = cleanPastedHtml(html)
    const read = data.getData.bind(data)
    Object.defineProperty(data, 'getData', {
      configurable: true,
      value: (type: string) => (type === 'text/html' ? cleaned : read(type)),
    })
  }
  holder.addEventListener('paste', clean, true)
  holder.addEventListener('drop', clean, true)
  return () => {
    holder.removeEventListener('paste', clean, true)
    holder.removeEventListener('drop', clean, true)
  }
}

const toolbarGap = 8

/** The top of the area the element is seen in: its nearest scrolling ancestor (the editor window's body), or 0. */
function visibleTop(element: HTMLElement): number {
  for (let node = element.parentElement; node; node = node.parentElement) {
    if (getComputedStyle(node).overflowY !== 'visible') return Math.max(0, node.getBoundingClientRect().top)
  }
  return 0
}

/**
 * Editor.js puts its inline toolbar under the selection; the panel of docs/ui/editor.md is above it, where it does
 * not cover the next lines. Each time Editor.js opens or moves the toolbar, it is moved above the selection, unless
 * there is no room above in the editor's visible area (the first line at its top edge): then it stays under. The placement
 * is on the toolbar as data-placement. The link field of a toolbar above opens upwards when it fits there, so it does
 * not cover the selected text (data-actions, BlockEditor styles).
 */
function placeToolbarAbove(holder: HTMLElement): () => void {
  const place = () => {
    const toolbar = holder.querySelector<HTMLElement>('.ce-inline-toolbar')
    const panel = toolbar?.querySelector<HTMLElement>('.ce-popover--inline > .ce-popover__container')
    const wrapper = toolbar?.offsetParent
    if (toolbar && panel) placeActions(toolbar, panel)
    const selection = window.getSelection()
    if (!toolbar || !panel || !wrapper || !selection || selection.rangeCount === 0 || selection.isCollapsed)
      return
    const range = selection.getRangeAt(0).getBoundingClientRect()
    if (range.width === 0 && range.height === 0) return
    const top = range.top - panel.getBoundingClientRect().height - toolbarGap
    const placement = top >= visibleTop(toolbar) ? 'top' : 'bottom'
    if (placement === 'top') {
      const value = `${Math.floor(top - wrapper.getBoundingClientRect().top)}px`
      if (toolbar.style.top !== value) toolbar.style.top = value
    }
    if (toolbar.dataset.placement !== placement) toolbar.dataset.placement = placement
  }
  const placeActions = (toolbar: HTMLElement, panel: HTMLElement) => {
    const actions = toolbar.querySelector<HTMLElement>('.ce-popover--nested-level-1 .ce-popover__container')
    if (!actions) return
    const room = panel.getBoundingClientRect().top - actions.getBoundingClientRect().height - toolbarGap
    const direction = toolbar.dataset.placement === 'top' && room >= visibleTop(toolbar) ? 'top' : 'bottom'
    if (toolbar.dataset.actions !== direction) toolbar.dataset.actions = direction
  }
  // Editor.js sets the toolbar's style when it opens and moves it; the observer runs before the next paint.
  const observer = new MutationObserver(place)
  observer.observe(holder, {
    subtree: true,
    childList: true,
    attributes: true,
    attributeFilter: ['style', 'hidden'],
  })
  return () => observer.disconnect()
}
