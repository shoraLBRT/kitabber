// The editing engine behind BlockEditor (E05-01, ADR-0026): Editor.js in the app, a fake in component tests. The
// component speaks the schema format (document.ts) and asks the engine only for what undo, redo and the caret need.
import type { OutputData } from '@editorjs/editorjs'
import { caretOffsetIn, placeCaret } from './caret'
import type { Caret } from './history'

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
  toolNames: { Text: 'Текст', Bold: 'Жирный', Italic: 'Курсив', Link: 'Ссылка' },
  tools: { link: { 'Add a link': 'Вставьте ссылку' } },
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
    // An empty paragraph is kept, so block indexes of the saved document match the editor's (undo, caret).
    tools: { paragraph: { inlineToolbar: true, config: { preserveBlank: true } } },
    onChange: () => onChange(),
  })
  await editor.isReady

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
    destroy: () => editor.destroy(),
  }
}
