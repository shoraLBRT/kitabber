// The quote block of the editor (E05-05, ADR-0023): the quoted text and the caption of its source, two editable fields
// stored apart (`text`, `caption`). Enter in the text goes to the caption (Shift+Enter breaks the line); Enter in the
// caption goes on with a text block after the quote. Backspace at the start of the caption goes back to the text, and
// at the start of an empty quote removes it (Editor.js, the quote's first and last inputs).
import type {
  API,
  BlockAPI,
  BlockTool,
  BlockToolConstructorOptions,
  ConversionConfig,
  ToolboxConfig,
} from '@editorjs/editorjs'
import { placeCaret } from './caret'

export interface QuoteData {
  text: string
  caption: string
}

const quoteIcon =
  '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 7H6a2 2 0 0 0-2 2v3h4v4H4"/><path d="M20 7h-4a2 2 0 0 0-2 2v3h4v4h-4"/></svg>'

/** Placeholders of the empty fields. */
export const quotePlaceholders = { text: 'Цитата', caption: 'Подпись, источник' } as const

/** A field with only `<br>` or spaces is empty: browsers leave a `<br>` in a field whose text was deleted. */
function fieldHtml(element: HTMLElement): string {
  const html = element.innerHTML
  const blank =
    html
      .replace(/<br\s*\/?>/gi, '')
      .replace(/&nbsp;/g, ' ')
      .trim() === ''
  return blank ? '' : html
}

export class QuoteTool implements BlockTool {
  static get toolbox(): ToolboxConfig {
    return { title: 'Quote', icon: quoteIcon }
  }

  /**
   * Text becomes the quote's text; a quote becomes text with its caption after a dash, so nothing is lost and the
   * text stays one line.
   */
  static get conversionConfig(): ConversionConfig {
    return {
      export: (data: QuoteData) => (data.caption ? `${data.text} — ${data.caption}` : data.text),
      import: 'text',
    }
  }

  // Enter is the quote's own (text → caption → next block), so Editor.js does not split the block on it.
  static get enableLineBreaks() {
    return true
  }

  // No sanitize of its own: Editor.js cleans both fields by the tags of the inline tools, as for headings.

  static get isReadOnlySupported() {
    return true
  }

  private readonly api: API
  private readonly block: BlockAPI | undefined
  private readonly element: HTMLElement
  private readonly text: HTMLElement
  private readonly caption: HTMLElement

  constructor({ data, api, block, readOnly }: BlockToolConstructorOptions<Partial<QuoteData>>) {
    this.api = api
    this.block = block
    this.element = document.createElement('blockquote')
    this.element.classList.add('cdx-quote')
    this.text = this.field('cdx-quote__text', quotePlaceholders.text, data?.text, readOnly)
    this.caption = this.field('cdx-quote__caption', quotePlaceholders.caption, data?.caption, readOnly)
    this.element.append(this.text, this.caption)
    if (!readOnly) this.element.addEventListener('keydown', (event) => this.onKeydown(event))
  }

  render(): HTMLElement {
    return this.element
  }

  save(element: HTMLElement): QuoteData {
    const text = element.querySelector<HTMLElement>('.cdx-quote__text')
    const caption = element.querySelector<HTMLElement>('.cdx-quote__caption')
    return { text: text ? fieldHtml(text) : '', caption: caption ? fieldHtml(caption) : '' }
  }

  // An empty quote is kept like an empty paragraph, so block indexes of the saved document match the editor's.
  validate(): boolean {
    return true
  }

  // No merge: Backspace at the start of the block after a quote puts the caret at the end of the caption instead of
  // joining body text into the source's caption.

  private field(className: string, placeholder: string, html: unknown, readOnly: boolean): HTMLElement {
    const element = document.createElement('div')
    element.classList.add(className)
    element.dataset.placeholder = placeholder
    element.contentEditable = readOnly ? 'false' : 'true'
    element.innerHTML = typeof html === 'string' ? html : ''
    return element
  }

  private onKeydown(event: KeyboardEvent) {
    if (event.key !== 'Enter' || event.isComposing) return
    const target = event.target as Node | null
    if (target && this.text.contains(target)) {
      // Shift+Enter is the browser's line break inside the text.
      if (event.shiftKey) return
      event.preventDefault()
      event.stopPropagation()
      placeCaret(this.caption, 0)
      return
    }
    if (target && this.caption.contains(target)) {
      // The caption is one line: Enter (with or without Shift) goes on with text after the quote.
      event.preventDefault()
      event.stopPropagation()
      const index = this.block
        ? this.api.blocks.getBlockIndex(this.block.id)
        : this.api.blocks.getCurrentBlockIndex()
      if (index < 0) return
      this.api.blocks.insert('paragraph', { text: '' }, undefined, index + 1, true)
      this.api.caret.setToBlock(index + 1, 'start')
    }
  }
}
