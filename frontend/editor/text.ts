// The text block of the editor (E05-02, ADR-0028, ADR-0044): the paragraph of Editor.js 2.31 with one change, its
// sanitize. The built-in paragraph declares a per-field config ({text: {br: true}}); Editor.js reads it per field when
// it saves a block, but as a flat list of tags when it converts or merges another block into a paragraph, so a heading
// turned into text lost its bold, italic, marker and links (#331). This one declares a flat list of the tags of its
// inline tools and `br`, which both readings understand.
import type {
  API,
  BlockTool,
  BlockToolConstructorOptions,
  ConversionConfig,
  PasteConfig,
  PasteEvent,
  SanitizerConfig,
  ToolboxConfig,
} from '@editorjs/editorjs'
import { LinkTool, MarkerTool } from './tools'

export interface TextData {
  text: string
}

export interface TextConfig {
  placeholder?: string
  /** Keep an empty paragraph on save, so block indexes of the saved document match the editor's. */
  preserveBlank?: boolean
}

const textIcon =
  '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-linecap="round" stroke-width="2" d="M8 9V7.2C8 7.08954 8.08954 7 8.2 7L12 7M16 9V7.2C16 7.08954 15.9105 7 15.8 7L12 7M12 7L12 17M12 17H10M12 17H14"/></svg>'

/**
 * The tags a text block keeps, with their attributes: bold and italic (built into Editor.js), the marker and the link
 * (tools.ts), and the line break. The same whitelist as the server's (ADR-0023).
 */
export function textTags(): Record<string, Record<string, unknown>> {
  return {
    b: {},
    i: {},
    ...(MarkerTool.sanitize as Record<string, Record<string, unknown>>),
    ...(LinkTool.sanitize as Record<string, Record<string, unknown>>),
    br: {},
  }
}

export class TextTool implements BlockTool {
  static get toolbox(): ToolboxConfig {
    return { icon: textIcon, title: 'Text' }
  }

  static get conversionConfig(): ConversionConfig {
    return { export: 'text', import: 'text' }
  }

  /**
   * Each tag maps to a function that returns its attributes. Editor.js merges the tags of the inline tools into every
   * object rule of a tool's config (as attributes); a function is passed as it is, and the HTML cleaner calls it for
   * the attributes of the tag.
   */
  static get sanitize(): SanitizerConfig {
    return Object.fromEntries(
      Object.entries(textTags()).map(([tag, attributes]) => [tag, () => attributes]),
    ) as unknown as SanitizerConfig
  }

  static get pasteConfig(): PasteConfig {
    return { tags: ['P'] }
  }

  static get isReadOnlySupported() {
    return true
  }

  private readonly api: API
  private readonly readOnly: boolean
  private readonly placeholder: string
  private readonly preserveBlank: boolean
  private data: TextData
  private element: HTMLElement | null = null

  constructor({ data, config, api, readOnly }: BlockToolConstructorOptions<Partial<TextData>, TextConfig>) {
    this.api = api
    this.readOnly = readOnly
    this.placeholder = config?.placeholder ?? ''
    this.preserveBlank = config?.preserveBlank ?? false
    this.data = { text: typeof data?.text === 'string' ? data.text : '' }
    this.onKeyUp = this.onKeyUp.bind(this)
  }

  render(): HTMLElement {
    const element = document.createElement('div')
    element.classList.add('ce-paragraph', this.api.styles.block)
    element.dataset.placeholderActive = this.api.i18n.t(this.placeholder)
    element.innerHTML = this.data.text
    element.contentEditable = this.readOnly ? 'false' : 'true'
    if (!this.readOnly) element.addEventListener('keyup', this.onKeyUp)
    this.element = element
    return element
  }

  /** A paragraph emptied by Backspace or Delete loses the `<br>` the browser leaves, so its placeholder shows. */
  private onKeyUp(event: KeyboardEvent) {
    if ((event.code !== 'Backspace' && event.code !== 'Delete') || !this.element) return
    if (this.element.textContent === '') this.element.innerHTML = ''
  }

  merge(data: TextData) {
    if (!this.element) return
    this.data.text += data.text
    const holder = document.createElement('div')
    holder.innerHTML = data.text.trim()
    this.element.append(...Array.from(holder.childNodes))
    this.element.normalize()
  }

  validate(data: TextData): boolean {
    return data.text.trim() !== '' || this.preserveBlank
  }

  save(element: HTMLElement): TextData {
    return { text: element.innerHTML }
  }

  onPaste(event: PasteEvent) {
    const detail = event.detail as { data: HTMLElement }
    this.data = { text: detail.data.innerHTML }
    window.requestAnimationFrame(() => {
      if (this.element) this.element.innerHTML = this.data.text
    })
  }
}
