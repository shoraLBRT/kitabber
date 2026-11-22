// The blocks of a post read for drawing (E07-03, ADR-0045): the stored document (ADR-0023, schema version 1) turned
// into what the renderer draws. A block of a type the renderer does not know, or with data it cannot read, becomes an
// `unknown` block drawn as a placeholder, so a document from a newer schema never breaks the page. Text fields go
// through cleanInlineHtml before v-html.
import { headerAnchor } from '../editor/document'
import { cleanInlineHtml } from './inline-html'

export interface ListNode {
  html: string
  children: ListNode[]
}

export type ViewBlock =
  | { kind: 'paragraph'; key: string; html: string }
  | { kind: 'header'; key: string; level: 2 | 3; html: string; anchor: string }
  | { kind: 'list'; key: string; ordered: boolean; items: ListNode[] }
  | { kind: 'quote'; key: string; html: string; caption: string }
  | { kind: 'delimiter'; key: string }
  | { kind: 'image'; key: string; media: string; width: number; height: number; caption: string }
  | { kind: 'unknown'; key: string; type: string }

type Data = Record<string, unknown>

const isString = (value: unknown): value is string => typeof value === 'string'

/** Flat items with levels (ADR-0033) as a tree: an item of level n + 1 goes under the closest item above it. */
export function listTree(items: string[], levels: number[]): ListNode[] {
  const top: ListNode[] = []
  const path: ListNode[] = []
  items.forEach((text, index) => {
    const node: ListNode = { html: cleanInlineHtml(text), children: [] }
    const level = Math.min(Math.max(0, levels[index] ?? 0), path.length)
    path.length = level
    ;(level === 0 ? top : path[level - 1]!.children).push(node)
    path.push(node)
  })
  return top
}

function readBlock(type: string, data: Data, key: string): ViewBlock | null {
  switch (type) {
    case 'paragraph':
      if (!isString(data.text)) return null
      return { kind: 'paragraph', key, html: cleanInlineHtml(data.text) }
    case 'header':
      if (!isString(data.text) || (data.level !== 2 && data.level !== 3)) return null
      return {
        kind: 'header',
        key,
        level: data.level,
        html: cleanInlineHtml(data.text),
        anchor: headerAnchor(key),
      }
    case 'list': {
      const { style, items, levels = [] } = data
      if (style !== 'ordered' && style !== 'unordered') return null
      if (!Array.isArray(items) || !items.every(isString) || !Array.isArray(levels)) return null
      if (!levels.every((level) => typeof level === 'number')) return null
      return { kind: 'list', key, ordered: style === 'ordered', items: listTree(items, levels as number[]) }
    }
    case 'quote':
      if (!isString(data.text)) return null
      return {
        kind: 'quote',
        key,
        html: cleanInlineHtml(data.text),
        caption: isString(data.caption) ? cleanInlineHtml(data.caption) : '',
      }
    case 'delimiter':
      return { kind: 'delimiter', key }
    case 'image': {
      const { media, width, height, caption } = data
      if (!isString(media) || !isSide(width) || !isSide(height)) return null
      return { kind: 'image', key, media, width, height, caption: isString(caption) ? cleanInlineHtml(caption) : '' }
    }
    default:
      return null
  }
}

/** A side of a picture in pixels: a positive whole number. */
function isSide(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0
}

/** The blocks of a stored document in order; anything that is not a document gives no blocks. */
export function viewBlocks(document: unknown): ViewBlock[] {
  const blocks = (document as { blocks?: unknown } | null)?.blocks
  if (!Array.isArray(blocks)) return []
  return blocks.map((block: unknown, index) => {
    const { id, type, data } = (block ?? {}) as { id?: unknown; type?: unknown; data?: unknown }
    const key = isString(id) && id.length > 0 ? id : `block-${index}`
    const kind = isString(type) ? type : ''
    const read = data && typeof data === 'object' ? readBlock(kind, data as Data, key) : null
    return read ?? { kind: 'unknown', key, type: kind }
  })
}
