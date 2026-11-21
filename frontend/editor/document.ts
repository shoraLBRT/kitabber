// Post document of the block editor (E05-01, ADR-0026): the format the server checks and stores (ADR-0023),
// `{version, blocks: [{id, type, data}]}`, and its conversion to and from the data of Editor.js. Editor.js adds service
// fields (`time`, its own `version`, block `tunes`) and keeps some blocks in a richer shape than schema version 1;
// toPostDocument drops and flattens them, toEditorData gives Editor.js back what it needs to render the document.
import type { OutputBlockData, OutputData } from '@editorjs/editorjs'
import { cleanInline } from './inline'

/** The schema version the client writes (ADR-0023, Platform.Modules.Editor BlockSchemas.V1). */
export const documentVersion = 1

export interface ParagraphBlock {
  id: string
  type: 'paragraph'
  data: { text: string }
}

export interface HeaderBlock {
  id: string
  type: 'header'
  data: { text: string; level: 2 | 3 }
}

export interface ListBlock {
  id: string
  type: 'list'
  /**
   * `levels` gives each item's nesting, 0 (top) or 1 (under the item above), or is empty when every item is at the
   * top (ADR-0033). The client writes it empty in that case, as the server's default for documents without it.
   */
  data: { style: 'unordered' | 'ordered'; items: string[]; levels: ListLevel[] }
}

/** Nesting of a list item: 0 at the top, 1 under the item above (E05-04, ADR-0033). */
export type ListLevel = 0 | 1

/** The deepest list level (Platform.Modules.Editor BlockSchemas.MaxListLevel). */
export const maxListLevel: ListLevel = 1

export interface QuoteBlock {
  id: string
  type: 'quote'
  data: { text: string; caption: string }
}

export interface DelimiterBlock {
  id: string
  type: 'delimiter'
  data: Record<string, never>
}

export type PostBlock = ParagraphBlock | HeaderBlock | ListBlock | QuoteBlock | DelimiterBlock

export type BlockType = PostBlock['type']

export interface PostDocument {
  version: number
  blocks: PostBlock[]
}

/** A block Editor.js gave that schema version 1 has no place for (a tool added without its schema). */
export class UnsupportedBlockError extends Error {
  constructor(
    readonly blockType: string,
    readonly blockId: string,
  ) {
    super(`Block type "${blockType}" (block ${blockId}) is not in schema version ${documentVersion}`)
    this.name = 'UnsupportedBlockError'
  }
}

export function emptyDocument(): PostDocument {
  return { version: documentVersion, blocks: [] }
}

/** True when the document has no block with any text (the "empty post" check of docs/ui/editor.md). */
export function isEmptyDocument(document: PostDocument): boolean {
  return document.blocks.every((block) => blockText(block).trim() === '')
}

/**
 * The anchor of a heading (ADR-0031): `h-` and the block id. The id is kept by the server and by undo and redo, so the
 * anchor stays while the heading's text changes; the reader page and a table of contents link to `#h-<id>`.
 */
export function headerAnchor(blockId: string): string {
  return `h-${blockId}`
}

/** Equal content: same blocks with the same ids, types and data. */
export function sameDocument(a: PostDocument, b: PostDocument): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

/** The block's text without markup, all fields joined: what the reader sees. */
export function blockText(block: PostBlock): string {
  switch (block.type) {
    case 'paragraph':
    case 'header':
      return stripTags(block.data.text)
    case 'quote':
      return stripTags(`${block.data.text} ${block.data.caption}`)
    case 'list':
      return stripTags(block.data.items.join(' '))
    case 'delimiter':
      return ''
  }
}

function stripTags(html: string): string {
  return html
    .replace(/<br\s*\/?>/gi, ' ')
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;/g, ' ')
}

/** Editor.js output to the schema format, fields in schema order, nothing else; text fields cleaned (cleanInline). */
export function toPostDocument(output: OutputData): PostDocument {
  return { version: documentVersion, blocks: output.blocks.map(toPostBlock) }
}

function toPostBlock(block: OutputBlockData): PostBlock {
  const id = block.id ?? ''
  const data = (block.data ?? {}) as Record<string, unknown>
  switch (block.type) {
    case 'paragraph':
      return { id, type: 'paragraph', data: { text: text(data.text) } }
    case 'header':
      return { id, type: 'header', data: { text: text(data.text), level: data.level === 3 ? 3 : 2 } }
    case 'list':
      return {
        id,
        type: 'list',
        data: {
          style: data.style === 'ordered' ? 'ordered' : 'unordered',
          ...listItems(data.items, data.levels),
        },
      }
    case 'quote':
      return { id, type: 'quote', data: { text: text(data.text), caption: text(data.caption) } }
    case 'delimiter':
      return { id, type: 'delimiter', data: {} }
    default:
      throw new UnsupportedBlockError(block.type, id)
  }
}

// Text fields hold inline HTML; whatever Editor.js kept outside the allow list of ADR-0023 is cleaned here, so the
// document the editor gives is the one the server stores (E05-02, ADR-0028).
function text(value: unknown): string {
  return typeof value === 'string' ? cleanInline(value) : ''
}

/**
 * Items and their levels in schema form (ADR-0033): the first item at the top, an item at most one level under the one
 * above and no deeper than maxListLevel; levels empty when every item is at the top.
 */
export function normalizeLevels(levels: readonly unknown[], count: number): ListLevel[] {
  const result: ListLevel[] = []
  for (let index = 0; index < count; index++) {
    const level = levels[index]
    const previous = index === 0 ? -1 : result[index - 1]!
    const wanted = typeof level === 'number' && Number.isInteger(level) ? level : 0
    result.push(Math.max(0, Math.min(wanted, previous + 1, maxListLevel)) as ListLevel)
  }
  return result.some((level) => level > 0) ? result : []
}

// The list tool of the editor (list.ts) gives strings and their levels. The list plugin of Editor.js (version 2) keeps
// items as `{content, meta, items}` with nesting; nested items follow their parent in reading order, and an item
// deeper than maxListLevel stays at that level.
function listItems(value: unknown, levels: unknown): { items: string[]; levels: ListLevel[] } {
  const items: string[] = []
  const given: number[] = []
  const walk = (list: unknown, depth: number) => {
    if (!Array.isArray(list)) return
    list.forEach((item: unknown, index) => {
      if (typeof item === 'string') {
        items.push(text(item))
        given.push(depth === 0 && Array.isArray(levels) ? Number(levels[index] ?? 0) : depth)
      } else if (item && typeof item === 'object') {
        const nested = item as { content?: unknown; items?: unknown }
        items.push(text(nested.content))
        given.push(depth)
        walk(nested.items, depth + 1)
      }
    })
  }
  walk(value, 0)
  return { items, levels: normalizeLevels(given, items.length) }
}

/** The schema format to the data Editor.js renders. Block ids are kept, so undo and redo keep them too. */
export function toEditorData(document: PostDocument): OutputData {
  return {
    blocks: document.blocks.map((block) => ({
      id: block.id,
      type: block.type,
      data: JSON.parse(JSON.stringify(block.data)) as Record<string, unknown>,
    })),
  }
}
