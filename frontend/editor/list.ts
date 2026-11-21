// The list block of the editor (E05-04, ADR-0033): «Маркированный список» and «Нумерованный список», one Editor.js
// tool with two entries in the block menu. Items nest one level deep (Tab, Shift+Tab); Enter adds an item, Enter in an
// empty item ends the list (or lifts a nested item to the top); the style switches in the block menu without losing
// the items or their levels. Each item is its own editable field, so Editor.js keeps its inline toolbar per item.
import type {
  API,
  BlockAPI,
  BlockTool,
  BlockToolConstructorOptions,
  ConversionConfig,
  ToolboxConfig,
} from '@editorjs/editorjs'
import { caretOffsetIn, placeCaret } from './caret'
import { maxListLevel, normalizeLevels, type ListLevel } from './document'
import { cleanInline } from './inline'

export type ListStyle = 'unordered' | 'ordered'

export interface ListData {
  style: ListStyle
  items: string[]
  levels: ListLevel[]
}

const svg = (paths: string) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`

const bulletIcon = svg(
  '<path d="M9 6h11"/><path d="M9 12h11"/><path d="M9 18h11"/><circle cx="4.5" cy="6" r="1"/><circle cx="4.5" cy="12" r="1"/><circle cx="4.5" cy="18" r="1"/>',
)
const numberIcon = svg(
  '<path d="M10 6h10"/><path d="M10 12h10"/><path d="M10 18h10"/><path d="M4 6h1v4"/><path d="M4 10h2"/><path d="M6 18H4c0-1 2-2 2-3s-1-1.5-2-1"/>',
)

/** Placeholder of an empty item. */
export const itemPlaceholder = 'Пункт списка'

/** Names of the two styles in the block menu (the tune that switches the style). */
export const styleTitles: Record<ListStyle, string> = {
  unordered: 'Маркированный',
  ordered: 'Нумерованный',
}

const itemClass = 'cdx-list__item'

/** The text of an item without markup: an item with only `<br>` or spaces is empty. */
function isBlank(html: string): boolean {
  return (
    html
      .replace(/<br\s*\/?>/gi, '')
      .replace(/&nbsp;/g, ' ')
      .trim() === ''
  )
}

// Converting a list to another tool gives its items as lines; converting back makes a line an item. Converting a list
// to the other list style goes through the same text, which would lose the levels and split items that have a line
// break, so the data of the list just exported is kept for the import that follows it in the same task (Editor.js
// exports and imports in one go) and used when the text is the same.
let exported: { text: string; data: ListData } | null = null

function linesOf(html: string): string[] {
  const lines = html.split(/<br\s*\/?>/i).map((line) => line.trim())
  const items = lines.filter((line) => !isBlank(line))
  return items.length > 0 ? items : ['']
}

function plain(html: string): string {
  return html
    .replace(/<[^>]*>/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

/** An entry of the block menu that switches the style (a MenuConfig item of Editor.js, not exported by its types). */
export interface StyleTune {
  icon: string
  title: string
  isActive: () => boolean
  closeOnActivate: boolean
  onActivate: () => void
}

export function listStyle(value: unknown): ListStyle {
  return value === 'ordered' ? 'ordered' : 'unordered'
}

export class ListTool implements BlockTool {
  static get toolbox(): ToolboxConfig {
    return [
      { title: 'Bulleted list', icon: bulletIcon, data: { style: 'unordered' } },
      { title: 'Numbered list', icon: numberIcon, data: { style: 'ordered' } },
    ]
  }

  static get conversionConfig(): ConversionConfig {
    return {
      export: (data: ListData) => {
        const text = data.items.join('<br>')
        exported = { text, data: JSON.parse(JSON.stringify(data)) as ListData }
        setTimeout(() => (exported = null), 0)
        return text
      },
      import: (text: string): ListData => {
        if (exported && plain(exported.text) === plain(text)) {
          // Cleaned as the text Editor.js would have imported: only the allowed markup (ADR-0028).
          const { items, levels } = exported.data
          exported = null
          return { style: 'unordered', items: items.map(cleanInline), levels }
        }
        return { style: 'unordered', items: linesOf(text), levels: [] }
      },
    }
  }

  // Enter and Tab are the list's own (new item, nesting), so Editor.js does not split the block on Enter.
  static get enableLineBreaks() {
    return true
  }

  static get isReadOnlySupported() {
    return true
  }

  private readonly api: API
  private readonly block: BlockAPI | undefined
  private readonly readOnly: boolean
  private style: ListStyle
  // Editor.js keeps the rendered element for the block's lifetime, so the `ul` or `ol` lives in a wrapper that stays
  // when the style switches.
  private readonly wrapper: HTMLElement
  private element: HTMLElement

  constructor({ data, api, block, readOnly }: BlockToolConstructorOptions<Partial<ListData>>) {
    this.api = api
    this.block = block
    this.readOnly = readOnly
    this.style = listStyle(data?.style)
    const items = Array.isArray(data?.items) && data.items.length > 0 ? data.items.map(String) : ['']
    const levels = normalizeLevels(Array.isArray(data?.levels) ? data.levels : [], items.length)
    this.wrapper = document.createElement('div')
    this.wrapper.classList.add('cdx-list-block')
    this.element = this.createList(this.style)
    items.forEach((html, index) => this.element.append(this.createItem(html, levels[index] ?? 0)))
    this.wrapper.append(this.element)
    if (!readOnly) this.wrapper.addEventListener('keydown', (event) => this.onKeydown(event))
  }

  render(): HTMLElement {
    return this.wrapper
  }

  save(element: HTMLElement): ListData {
    const items = [...element.querySelectorAll<HTMLElement>(`.${itemClass}`)]
    const texts = items.map((item) => item.innerHTML)
    const levels = normalizeLevels(
      items.map((item) => Number(item.dataset.level ?? 0)),
      items.length,
    )
    return { style: this.style, items: texts, levels }
  }

  // An empty list is kept like an empty paragraph, so block indexes of the saved document match the editor's.
  validate(): boolean {
    return true
  }

  /** Text merged into the list (Backspace at the start of the next block) joins its last item. */
  merge(data: Partial<ListData>) {
    const incoming = Array.isArray(data.items) ? data.items : []
    if (incoming.length === 0) return
    const items = this.items()
    const last = items[items.length - 1]
    const levels = Array.isArray(data.levels) ? data.levels : []
    incoming.forEach((html, index) => {
      if (index === 0 && last) last.innerHTML += html
      else this.element.append(this.createItem(html, (levels[index] ?? 0) as ListLevel))
    })
    this.fixLevels()
  }

  renderSettings(): StyleTune[] {
    return (['unordered', 'ordered'] as const).map((style) => ({
      icon: style === 'ordered' ? numberIcon : bulletIcon,
      title: styleTitles[style],
      isActive: () => this.style === style,
      closeOnActivate: true,
      onActivate: () => this.setStyle(style),
    }))
  }

  /** Switches between bullets and numbers; the items and their levels stay. */
  setStyle(style: ListStyle) {
    if (style === this.style) return
    this.style = style
    const next = this.createList(style)
    next.append(...this.element.childNodes)
    this.element.replaceWith(next)
    this.element = next
    this.fixLevels()
    this.block?.dispatchChange()
  }

  private createList(style: ListStyle) {
    const list = document.createElement(style === 'ordered' ? 'ol' : 'ul')
    list.classList.add('cdx-list', `cdx-list--${style}`)
    list.dataset.style = style
    return list
  }

  private createItem(html: string, level: ListLevel): HTMLLIElement {
    const item = document.createElement('li')
    item.classList.add(itemClass)
    item.dataset.level = String(level)
    item.dataset.placeholder = itemPlaceholder
    item.contentEditable = this.readOnly ? 'false' : 'true'
    item.innerHTML = html
    return item
  }

  private items(): HTMLElement[] {
    return [...this.element.querySelectorAll<HTMLElement>(`.${itemClass}`)]
  }

  // A nested item that lost its parent level (the item above moved up or away) moves up with it.
  private fixLevels() {
    const items = this.items()
    const levels = normalizeLevels(
      items.map((item) => Number(item.dataset.level ?? 0)),
      items.length,
    )
    items.forEach((item, index) => (item.dataset.level = String(levels[index] ?? 0)))
  }

  private level(item: HTMLElement): ListLevel {
    return item.dataset.level === '1' ? 1 : 0
  }

  private setLevel(item: HTMLElement, level: ListLevel) {
    const offset = caretOffsetIn(item, window.getSelection())
    item.dataset.level = String(level)
    this.fixLevels()
    placeCaret(item, offset ?? 0)
    this.block?.dispatchChange()
  }

  private onKeydown(event: KeyboardEvent) {
    if (event.isComposing) return
    const item = (event.target as HTMLElement | null)?.closest<HTMLElement>(`.${itemClass}`)
    if (!item || !this.element.contains(item)) return
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      event.stopPropagation()
      this.enter(item)
    } else if (event.key === 'Enter') {
      // Shift+Enter is a line break inside the item.
      event.preventDefault()
      event.stopPropagation()
      document.execCommand('insertLineBreak')
    } else if (event.key === 'Tab' && !event.altKey && !event.ctrlKey && !event.metaKey) {
      event.preventDefault()
      event.stopPropagation()
      if (event.shiftKey) this.outdent(item)
      else this.indent(item)
    } else if (event.key === 'Backspace' && this.caretAtStart(item)) {
      this.backspace(item, event)
    }
  }

  /** Tab: the item goes under the one above; the first item and an already nested one stay. */
  indent(item: HTMLElement) {
    const index = this.items().indexOf(item)
    if (index <= 0 || this.level(item) >= maxListLevel) return
    this.setLevel(item, 1)
  }

  /** Shift+Tab: a nested item goes back to the top. */
  outdent(item: HTMLElement) {
    if (this.level(item) === 0) return
    this.setLevel(item, 0)
  }

  /**
   * Enter: in an empty nested item it goes to the top; in an empty top item the list ends — the item becomes a text
   * block after the list, and the items below it, if any, a new list after that. Otherwise the item splits at the
   * caret and the caret goes to the new item.
   */
  enter(item: HTMLElement) {
    if (isBlank(item.innerHTML)) {
      if (this.level(item) > 0) {
        this.setLevel(item, 0)
        return
      }
      this.endList(item)
      return
    }
    const selection = window.getSelection()
    let rest = ''
    if (selection && selection.rangeCount > 0) {
      const range = selection.getRangeAt(0)
      range.deleteContents()
      const tail = document.createRange()
      tail.selectNodeContents(item)
      tail.setStart(range.endContainer, range.endOffset)
      const holder = document.createElement('div')
      holder.append(tail.extractContents())
      rest = isBlank(holder.innerHTML) ? '' : holder.innerHTML
    }
    const next = this.createItem(rest, this.level(item))
    item.after(next)
    next.focus()
    placeCaret(next, 0)
    this.block?.dispatchChange()
  }

  private endList(item: HTMLElement) {
    const items = this.items()
    const index = items.indexOf(item)
    const blockIndex = this.blockIndex()
    if (blockIndex < 0) return
    const after = items.slice(index + 1)
    const rest: ListData | null =
      after.length > 0
        ? {
            style: this.style,
            items: after.map((element) => element.innerHTML),
            levels: normalizeLevels(
              after.map((element) => Number(element.dataset.level ?? 0)),
              after.length,
            ),
          }
        : null
    item.remove()
    after.forEach((element) => element.remove())
    if (index === 0) {
      // Nothing above: the list itself becomes the text block.
      this.api.blocks.insert('paragraph', { text: '' }, undefined, blockIndex, true, true)
      if (rest) this.api.blocks.insert('list', rest, undefined, blockIndex + 1, false)
      this.api.caret.setToBlock(blockIndex, 'start')
      return
    }
    this.block?.dispatchChange()
    this.api.blocks.insert('paragraph', { text: '' }, undefined, blockIndex + 1, true)
    if (rest) this.api.blocks.insert('list', rest, undefined, blockIndex + 2, false)
    this.api.caret.setToBlock(blockIndex + 1, 'start')
  }

  /**
   * Backspace at the start of an item: a nested item goes to the top; a top item joins the one above. The first item
   * is left to Editor.js, which joins the list with the block above or removes it when empty.
   */
  private backspace(item: HTMLElement, event: KeyboardEvent) {
    const items = this.items()
    const index = items.indexOf(item)
    if (this.level(item) > 0) {
      event.preventDefault()
      event.stopPropagation()
      this.setLevel(item, 0)
      return
    }
    if (index <= 0) return
    event.preventDefault()
    event.stopPropagation()
    const previous = items[index - 1]!
    const offset = (previous.textContent ?? '').length
    const html = isBlank(item.innerHTML) ? '' : item.innerHTML
    if (isBlank(previous.innerHTML)) previous.innerHTML = html
    else previous.innerHTML += html
    item.remove()
    this.fixLevels()
    previous.focus()
    placeCaret(previous, offset)
    this.block?.dispatchChange()
  }

  private caretAtStart(item: HTMLElement): boolean {
    const selection = window.getSelection()
    return !!selection?.isCollapsed && caretOffsetIn(item, selection) === 0
  }

  private blockIndex(): number {
    return this.block ? this.api.blocks.getBlockIndex(this.block.id) : this.api.blocks.getCurrentBlockIndex()
  }
}
