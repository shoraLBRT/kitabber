// The heading blocks of the editor (E05-03, ADR-0031): «Заголовок» (H2) and «Подзаголовок» (H3), one Editor.js tool
// with two entries in the block menu. A heading converts to and from a text block by its text (conversionConfig), so
// the content and its inline markup stay. Each heading carries its anchor (headerAnchor) as the element id.
import type {
  BlockTool,
  BlockToolConstructorOptions,
  ConversionConfig,
  ToolboxConfig,
} from '@editorjs/editorjs'
import { headerAnchor } from './document'
import { t } from '../i18n'

export type HeaderLevel = 2 | 3

export interface HeaderData {
  text: string
  level: HeaderLevel
}

const icon = (label: string) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 12h8"/><path d="M4 18V6"/><path d="M12 18V6"/>${label}</svg>`

const h2Icon = icon('<path d="M21 18h-4c0-4 4-3 4-6 0-1.5-2-2.5-4-1"/>')
const h3Icon = icon(
  '<path d="M17.5 10.5c1.7-1 3.5 0 3.5 1.5a2 2 0 0 1-2 2"/><path d="M17 17.5c2 1.5 4 .3 4-1.5a2 2 0 0 0-2-2"/>',
)

/** The placeholder of an empty heading, by level. */
export const headerPlaceholders: Record<HeaderLevel, string> = {
  2: t('editor.heading'),
  3: t('editor.subheading'),
}

export function headerLevel(value: unknown): HeaderLevel {
  return value === 3 ? 3 : 2
}

export class HeaderTool implements BlockTool {
  static get toolbox(): ToolboxConfig {
    return [
      { title: 'Heading', icon: h2Icon, data: { level: 2 } },
      { title: 'Subheading', icon: h3Icon, data: { level: 3 } },
    ]
  }

  static get conversionConfig(): ConversionConfig {
    return { export: 'text', import: 'text' }
  }

  // No sanitize of its own: Editor.js then cleans the heading by the tags of its inline tools, and it cleans text
  // converted to a heading by the same config. A per-field config ({text: {…}}) would be read on conversion as a list
  // of tags (Editor.js 2.31) and drop all the markup.

  static get isReadOnlySupported() {
    return true
  }

  private readonly element: HTMLHeadingElement
  private readonly level: HeaderLevel

  constructor({ data, block, readOnly }: BlockToolConstructorOptions<Partial<HeaderData>>) {
    this.level = headerLevel(data?.level)
    this.element = document.createElement(this.level === 3 ? 'h3' : 'h2')
    this.element.classList.add('ce-header')
    this.element.dataset.level = String(this.level)
    this.element.dataset.placeholder = headerPlaceholders[this.level]
    this.element.contentEditable = readOnly ? 'false' : 'true'
    this.element.innerHTML = typeof data?.text === 'string' ? data.text : ''
    if (block?.id) this.element.id = headerAnchor(block.id)
  }

  render(): HTMLElement {
    return this.element
  }

  save(element: HTMLElement): HeaderData {
    return { text: element.innerHTML, level: this.level }
  }
}
