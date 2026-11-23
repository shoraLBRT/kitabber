import { describe, expect, it } from 'vitest'
import { isEmptyDocument } from '../../editor/document'
import { blockId, quickParagraphs, quickPostDocument, textToInline } from '../../editor/quick-post'

// The quick post's text and photos as the post document of the full editor (E05-29, ADR-0055).
describe('quick post document', () => {
  const ids = () => {
    let n = 0
    return () => `b${++n}`
  }

  it('splits the text into paragraphs at empty lines and keeps a single line break', () => {
    expect(quickParagraphs('  Первый\nабзац \n\n\n Второй  \r\n\r\nТретий\n')).toEqual([
      'Первый\nабзац',
      'Второй',
      'Третий',
    ])
    expect(quickParagraphs(' \n\n  ')).toEqual([])
  })

  it('escapes the markup characters, so typed text never becomes tags', () => {
    expect(textToInline('<b>a & "b"</b>\nдальше')).toBe('&lt;b&gt;a &amp; &quot;b&quot;&lt;/b&gt;<br>дальше')
  })

  it('puts the text first and the photos after it in the order attached', () => {
    const document = quickPostDocument(
      'Привет\n\nмир',
      [
        { media: 'm1', width: 40, height: 30 },
        { media: 'm2', width: 10, height: 20 },
      ],
      ids(),
    )
    expect(document).toEqual({
      version: 1,
      blocks: [
        { id: 'b1', type: 'paragraph', data: { text: 'Привет' } },
        { id: 'b2', type: 'paragraph', data: { text: 'мир' } },
        { id: 'b3', type: 'image', data: { media: 'm1', width: 40, height: 30, caption: '' } },
        { id: 'b4', type: 'image', data: { media: 'm2', width: 10, height: 20, caption: '' } },
      ],
    })
  })

  it('is empty without text and photos, and not empty with a photo alone', () => {
    expect(isEmptyDocument(quickPostDocument('   \n ', []))).toBe(true)
    expect(isEmptyDocument(quickPostDocument('', [{ media: 'm1', width: 1, height: 1 }]))).toBe(false)
  })

  it('makes block ids the server takes: ten Latin letters and digits, different each time', () => {
    const made = new Set(Array.from({ length: 50 }, () => blockId()))
    expect(made.size).toBe(50)
    for (const id of made) expect(id).toMatch(/^[A-Za-z0-9]{10}$/)
  })
})
