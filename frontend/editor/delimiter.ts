// The delimiter block of the editor (E05-05, ADR-0023): a line between parts of a post with no content (`data: {}`).
// It has no editable field, so Editor.js treats it as an empty block: Backspace at the start of the block after it,
// or Delete at the end of the block before it, removes it. Arrows and a click select it; Backspace or Delete then
// removes it and puts the caret next to where it was (removeSelectedDelimiter), instead of Editor.js's default of
// leaving an empty text block in its place.
import type { API, BlockAPI, BlockTool, BlockToolConstructorOptions, ToolboxConfig } from '@editorjs/editorjs'

const delimiterIcon =
  '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 12h16"/><path d="M8 6h8"/><path d="M8 18h8"/></svg>'

/** The class of the delimiter's element; the engine finds a selected delimiter by it. */
export const delimiterClass = 'ce-delimiter'

/** What a screen reader says for the delimiter (role separator). */
export const delimiterLabel = 'Разделитель'

export class DelimiterTool implements BlockTool {
  static get toolbox(): ToolboxConfig {
    return { title: 'Delimiter', icon: delimiterIcon }
  }

  static get isReadOnlySupported() {
    return true
  }

  private readonly element: HTMLElement

  constructor({ api, block, readOnly }: BlockToolConstructorOptions) {
    // An empty element, decorated by styles: text or an <hr> inside would make Editor.js count the block as not empty.
    this.element = document.createElement('div')
    this.element.classList.add(delimiterClass)
    this.element.setAttribute('role', 'separator')
    this.element.setAttribute('aria-label', delimiterLabel)
    this.element.contentEditable = 'false'
    if (!readOnly) this.element.addEventListener('click', () => select(api, block))
  }

  render(): HTMLElement {
    return this.element
  }

  save(): Record<string, never> {
    return {}
  }
}

/** Selects the delimiter's block (the caret API selects a block without editable fields). */
function select(api: API, block: BlockAPI | undefined) {
  if (!block) return
  const index = api.blocks.getBlockIndex(block.id)
  if (index >= 0) api.caret.setToBlock(index)
}

/**
 * Backspace or Delete with one selected delimiter removes just it: the caret goes to the end of the block before it
 * (Backspace) or the start of the block after it (Delete), or to the other side when there is none. Listens on the
 * window in the capture phase, before Editor.js's document handler, which would put an empty text block in its place.
 */
export function removeSelectedDelimiter(holder: HTMLElement, api: Pick<API, 'blocks' | 'caret'>): () => void {
  const onKeydown = (event: KeyboardEvent) => {
    if (event.key !== 'Backspace' && event.key !== 'Delete') return
    if (event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return
    const selected = [...holder.querySelectorAll<HTMLElement>('.ce-block--selected')]
    const only = selected.length === 1 ? selected[0] : undefined
    if (!only?.dataset.id || !only.querySelector(`.${delimiterClass}`)) return
    const index = api.blocks.getBlockIndex(only.dataset.id)
    if (index === undefined || index < 0) return
    event.preventDefault()
    event.stopImmediatePropagation()
    api.blocks.delete(index)
    const count = api.blocks.getBlocksCount()
    if (count === 0) return
    const backward = event.key === 'Backspace'
    if ((backward && index > 0) || index >= count) api.caret.setToBlock(Math.min(index - 1, count - 1), 'end')
    else api.caret.setToBlock(index, 'start')
  }
  window.addEventListener('keydown', onKeydown, true)
  return () => window.removeEventListener('keydown', onKeydown, true)
}

/**
 * After a delimiter is added from the block menu, the caret goes to the block after it, so writing goes on below the
 * line; a text block is added when the delimiter is the last block. Called with the blocks Editor.js reports as added.
 */
export function continueAfterDelimiter(api: Pick<API, 'blocks' | 'caret'>, added: BlockAPI) {
  if (added.name !== 'delimiter') return
  const index = api.blocks.getBlockIndex(added.id)
  if (index === undefined || index < 0) return
  if (index === api.blocks.getBlocksCount() - 1)
    api.blocks.insert('paragraph', { text: '' }, undefined, index + 1, true)
  api.caret.setToBlock(index + 1, 'start')
}
