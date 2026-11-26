// The editing engine behind BlockEditor (E05-01, ADR-0026): Editor.js in the app, a fake in component tests. The
// component speaks the schema format (document.ts) and asks the engine only for what undo, redo and the caret need.
import type { OutputData, ToolConstructable } from '@editorjs/editorjs'
import { caretOffsetIn, placeCaret } from './caret'
import { continueAfterDelimiter, DelimiterTool, removeSelectedDelimiter } from './delimiter'
import type { Caret } from './history'
import { HeaderTool } from './header'
import { cleanPastedHtml } from './inline'
import { ListTool } from './list'
import { searchOnPhone } from './menu'
import { moveBlocks } from './move'
import { ImageTool, type ImageToolConfig, type ImageUpload } from './image'
import { QuoteTool } from './quote'
import { TextTool } from './text'
import { LinkTool, MarkerTool } from './tools'
import { t } from '../i18n'

export interface EngineOptions {
  holder: HTMLElement
  data: OutputData
  placeholder: string
  /** Called after the content changed (typing, a block added or removed). */
  onChange: () => void
  /** Uploads a picked picture for the «Фото» block (E05-06); without it the block reports every upload failed. */
  uploadImage?: (file: File) => Promise<ImageUpload>
}

export interface EditorEngine {
  save(): Promise<OutputData>
  render(data: OutputData): Promise<void>
  /** The block with the caret and the text offset in it, or null when the caret is outside the editor. */
  caret(): Caret | null
  setCaret(caret: Caret): void
  /** Opens the block menu at the caret's block (at the last block when the caret is outside): the phone's «+». */
  openBlockMenu(): void
  /** Makes the selection bold or italic, or takes it off, as the floating panel does: the phone's «Ж» and «К». */
  format(mark: InlineMark): void
  /** Adds a «Фото» block after the caret's block (at the end when the caret is outside) and uploads `file` into it. */
  addImage(file: File): void
  destroy(): void
}

export type InlineMark = 'bold' | 'italic'

export type CreateEngine = (options: EngineOptions) => Promise<EditorEngine>

// The labels of the Editor.js interface, from the dictionary (E02-10). Tools of later tasks add theirs.
const messages = {
  ui: {
    blockTunes: { toggler: { 'Click to tune': t('editor.tune'), 'or drag to move': t('editor.dragToMove') } },
    inlineToolbar: { converter: { 'Convert to': t('editor.convertTo') } },
    toolbar: { toolbox: { Add: t('editor.addBlock') } },
    popover: {
      Filter: t('editor.search'),
      'Nothing found': t('editor.nothingFound'),
      'Convert to': t('editor.convertTo'),
    },
  },
  toolNames: {
    Text: t('editor.text'),
    Heading: t('editor.heading'),
    Subheading: t('editor.subheading'),
    'Bulleted list': t('editor.bulletedList'),
    'Numbered list': t('editor.numberedList'),
    Quote: t('editor.quote'),
    Delimiter: t('editor.delimiter'),
    Image: t('editor.image'),
    Bold: t('editor.bold'),
    Italic: t('editor.italic'),
    Marker: t('editor.marker'),
    Link: t('editor.link'),
  },
  blockTunes: {
    delete: { Delete: t('editor.delete'), 'Click to delete': t('editor.clickToDelete') },
    moveUp: { 'Move up': t('editor.moveUp') },
    moveDown: { 'Move down': t('editor.moveDown') },
  },
}

/** Editor.js, loaded on demand: it touches the DOM, so it never runs on the server. */
export const createEditorJs: CreateEngine = async ({ holder, data, placeholder, onChange, uploadImage }) => {
  const { default: EditorJS } = await import('@editorjs/editorjs')
  const editor = new EditorJS({
    holder,
    data,
    placeholder,
    autofocus: true,
    minHeight: 0,
    i18n: { messages },
    tools: {
      // The text block in place of the built-in paragraph, whose sanitize drops markup converted into it (ADR-0044).
      // An empty paragraph is kept, so block indexes of the saved document match the editor's (undo, caret).
      paragraph: {
        class: TextTool as unknown as ToolConstructable,
        inlineToolbar: ['bold', 'italic', 'marker', 'link'],
        config: { preserveBlank: true },
      },
      // «Заголовок» (H2) and «Подзаголовок» (H3), E05-03: one tool with two entries in the block menu. The same inline
      // tools as the text block: Editor.js cleans converted text by the target's tools, so text↔heading keeps markup.
      header: {
        class: HeaderTool as unknown as ToolConstructable,
        inlineToolbar: ['bold', 'italic', 'marker', 'link'],
      },
      // «Маркированный список» and «Нумерованный список», E05-04: one tool with two entries, items nest one level.
      list: {
        class: ListTool as unknown as ToolConstructable,
        inlineToolbar: ['bold', 'italic', 'marker', 'link'],
      },
      // «Цитата», E05-05: the text and the caption of its source in two fields, each with the inline tools.
      quote: {
        class: QuoteTool as unknown as ToolConstructable,
        inlineToolbar: ['bold', 'italic', 'marker', 'link'],
      },
      // «Разделитель», E05-05: no content; removed by one key (delimiter.ts).
      delimiter: { class: DelimiterTool as unknown as ToolConstructable },
      // «Фото», E05-06: one uploaded image with a caption (image.ts).
      image: {
        class: ImageTool as unknown as ToolConstructable,
        inlineToolbar: ['bold', 'italic', 'marker', 'link'],
        config: { upload: uploadImage ?? (async () => ({ status: 'error' })) } satisfies ImageToolConfig,
      },
      marker: { class: MarkerTool as unknown as ToolConstructable, shortcut: 'CMD+SHIFT+M' },
      // The same name as the built-in link tool, so it takes its place.
      link: { class: LinkTool as unknown as ToolConstructable },
    },
    onChange: (api, event) => {
      for (const change of Array.isArray(event) ? event : [event]) {
        if (change.type === 'block-added') continueAfterDelimiter(api, change.detail.target)
      }
      onChange()
    },
  })
  await editor.isReady
  const stopCleaning = cleanTransfers(holder)
  const stopPlacing = placeToolbarAbove(holder)
  const stopRemoving = removeSelectedDelimiter(holder, editor)
  const stopMoving = moveBlocks(holder, editor)
  const stopSearching = searchOnPhone(holder, messages.ui.popover.Filter)
  const stopMarking = markAtCaret(holder)

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
    openBlockMenu() {
      if (editor.blocks.getBlocksCount() === 0) return
      if (editor.blocks.getCurrentBlockIndex() < 0) editor.caret.setToLastBlock('end')
      editor.toolbar.toggleToolbox(true)
    },
    format(mark) {
      // The bold and italic tools built into Editor.js do the same; only a selection inside the blocks is changed.
      const selection = window.getSelection()
      const node = selection && selection.rangeCount > 0 ? selection.anchorNode : null
      if (node && holder.contains(node)) window.document.execCommand(mark)
    },
    addImage(file) {
      const current = editor.blocks.getCurrentBlockIndex()
      const index = current < 0 ? editor.blocks.getBlocksCount() : current + 1
      const block = editor.blocks.insert('image', {}, undefined, index, false)
      block.call('uploadFile', file as unknown as object)
    },
    destroy() {
      stopCleaning()
      stopPlacing()
      stopRemoving()
      stopMoving()
      stopSearching()
      stopMarking()
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

const caretMarks: Record<string, InlineMark> = { KeyB: 'bold', KeyI: 'italic' }

/**
 * Ctrl+B and Ctrl+I (Cmd on a Mac) with no selection turn bold or italic on for the text typed next (#387). Editor.js
 * reads these keys on the document and, once a block is its current one, prevents the browser's own command and asks
 * its inline toolbar instead; with a bare caret the toolbar is not open, so the key was lost — in a new list item as
 * soon as Editor.js had made the list current (a pause after Enter). The command is given here, before Editor.js sees
 * the key; a selection is still Editor.js's (its toolbar is open then). The keys are read by position, so they work
 * in the Russian layout too.
 */
export function markAtCaret(holder: HTMLElement): () => void {
  const mark = (event: KeyboardEvent) => {
    if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey || event.isComposing) return
    const name = caretMarks[event.code]
    const target = event.target instanceof HTMLElement ? event.target : null
    const selection = window.getSelection()
    if (!name || !target?.isContentEditable || !selection?.isCollapsed || selection.rangeCount === 0) return
    if (!target.contains(selection.anchorNode)) return
    event.preventDefault()
    event.stopPropagation()
    window.document.execCommand(name)
  }
  holder.addEventListener('keydown', mark, true)
  return () => holder.removeEventListener('keydown', mark, true)
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
