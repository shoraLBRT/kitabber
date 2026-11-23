// The quick post (E05-29, FR-EDITOR «Быстрый пост», ADR-0055): the compact form above the feed writes plain text and
// attached photos, and both become the same post document the full editor writes (ADR-0023). The text is split into
// paragraphs at empty lines, a single line break stays a <br> in its paragraph, and the photos follow the text as
// «Фото» blocks (ADR-0049) in the order they were attached. «Развернуть» hands this document to the full editor, so
// nothing typed or attached is lost on the way.
import { documentVersion, type ImageBlock, type ParagraphBlock, type PostDocument } from './document'

/** An attached photo the media service has taken: its file id and its size in pixels. */
export interface QuickPhoto {
  media: string
  width: number
  height: number
}

/** What the form hands to the full editor: the document and the chosen topic (`null` is «Без темы»). */
export interface QuickPostHandoff {
  document: PostDocument
  topicId: string | null
}

/** The most photos one quick post carries; more go through the full editor. */
export const quickPhotoLimit = 10

const escapes: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }

/** Plain text as inline HTML: the markup characters escaped, a line break as <br>. */
export function textToInline(text: string): string {
  return text.replace(/[&<>"]/g, (character) => escapes[character]!).replace(/\n/g, '<br>')
}

/** The text's paragraphs: split at empty lines, each trimmed, the empty ones dropped. */
export function quickParagraphs(text: string): string[] {
  return text
    .replace(/\r\n?/g, '\n')
    .split(/\n[ \t]*\n/)
    .map((paragraph) => paragraph.replace(/^\s+|\s+$/g, ''))
    .filter((paragraph) => paragraph !== '')
}

/** A block id as Editor.js makes them: ten Latin letters and digits (BlockDocumentValidator takes 1–64 of them). */
export function blockId(random: () => number = Math.random): string {
  const alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
  let id = ''
  for (let i = 0; i < 10; i++) id += alphabet[Math.floor(random() * alphabet.length)]
  return id
}

/** The post document of the text and the photos, the text first. */
export function quickPostDocument(
  text: string,
  photos: readonly QuickPhoto[],
  makeId: () => string = blockId,
): PostDocument {
  const paragraphs = quickParagraphs(text).map((paragraph): ParagraphBlock => ({
    id: makeId(),
    type: 'paragraph',
    data: { text: textToInline(paragraph) },
  }))
  const images = photos.map((photo): ImageBlock => ({
    id: makeId(),
    type: 'image',
    data: { media: photo.media, width: photo.width, height: photo.height, caption: '' },
  }))
  return { version: documentVersion, blocks: [...paragraphs, ...images] }
}
