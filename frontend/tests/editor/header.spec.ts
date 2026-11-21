import type { BlockToolConstructorOptions } from '@editorjs/editorjs'
import { describe, expect, it } from 'vitest'
import { headerAnchor, toEditorData, toPostDocument, type PostDocument } from '../../editor/document'
import { HeaderTool, headerPlaceholders, type HeaderData } from '../../editor/header'

// Heading blocks (E05-03, #38): «Заголовок» (H2) and «Подзаголовок» (H3) with stable anchors (ADR-0031), converted to
// and from the text block by their text.

function tool(data: Partial<HeaderData>, id = 'b1', readOnly = false) {
  return new HeaderTool({
    data,
    block: { id },
    readOnly,
  } as unknown as BlockToolConstructorOptions<Partial<HeaderData>>)
}

describe('HeaderTool', () => {
  it('offers «Заголовок» (H2) and «Подзаголовок» (H3) in the block menu', () => {
    const entries = HeaderTool.toolbox as { title: string; data: { level: number } }[]
    expect(entries.map((entry) => entry.data.level)).toEqual([2, 3])
    expect(entries.map((entry) => entry.title)).toEqual(['Heading', 'Subheading'])
  })

  it.each([
    [2, 'H2', 'Заголовок'],
    [3, 'H3', 'Подзаголовок'],
  ] as const)('renders level %i as %s with its placeholder', (level, tag, placeholder) => {
    const element = tool({ text: 'Глава', level }).render()
    expect(element.tagName).toBe(tag)
    expect(element.textContent).toBe('Глава')
    expect(element.dataset.placeholder).toBe(placeholder)
    expect(element.getAttribute('contenteditable')).toBe('true')
    expect(headerPlaceholders[level]).toBe(placeholder)
  })

  it('takes level 2 when the level is missing or unknown, e.g. converted from text', () => {
    expect(tool({ text: 'Глава' }).render().tagName).toBe('H2')
    expect(tool({ text: 'Глава', level: 4 as 2 }).render().tagName).toBe('H2')
  })

  it('gives the heading its anchor from the block id', () => {
    expect(tool({ text: 'Глава', level: 2 }, 'Xy-12_ab').render().id).toBe('h-Xy-12_ab')
    expect(headerAnchor('Xy-12_ab')).toBe('h-Xy-12_ab')
  })

  it('keeps the anchor while the text changes: it does not depend on the text', () => {
    const before = tool({ text: 'Старое название', level: 2 }, 'b7').render().id
    const after = tool({ text: 'Новое <i>название</i>', level: 2 }, 'b7').render().id
    expect(after).toBe(before)
  })

  it('saves the text with its inline markup and the level', () => {
    const heading = tool({ text: 'Глава <b>первая</b>', level: 3 })
    const element = heading.render()
    element.innerHTML += ' и <mark>вторая</mark>'
    expect(heading.save(element)).toEqual({ text: 'Глава <b>первая</b> и <mark>вторая</mark>', level: 3 })
  })

  it('converts to and from text by the text field', () => {
    expect(HeaderTool.conversionConfig).toEqual({ export: 'text', import: 'text' })
    // Without its own sanitize config Editor.js keeps the tags of the inline tools on conversion.
    expect('sanitize' in HeaderTool).toBe(false)
  })

  it('is not editable in read-only mode', () => {
    expect(tool({ text: 'Глава', level: 2 }, 'b1', true).render().getAttribute('contenteditable')).toBe(
      'false',
    )
  })
})

describe('heading in the document', () => {
  const document: PostDocument = {
    version: 1,
    blocks: [
      { id: 'h1', type: 'header', data: { text: 'Глава <i>первая</i>', level: 2 } },
      { id: 'h2', type: 'header', data: { text: 'Раздел', level: 3 } },
    ],
  }

  it('goes to the editor and back unchanged, ids (and so anchors) kept', () => {
    expect(toPostDocument(toEditorData(document))).toEqual(document)
  })

  it('a text block converted to a heading keeps its text and markup', () => {
    // What Editor.js does on conversion: the text exported from the paragraph is imported as the heading's text.
    const paragraph = { text: 'Обычный <b>жирный</b> текст' }
    const heading = tool({ text: paragraph.text, level: 2 })
    const saved = heading.save(heading.render())
    expect(saved.text).toBe(paragraph.text)
    expect(toPostDocument({ blocks: [{ id: 'p1', type: 'header', data: { ...saved } }] }).blocks[0]).toEqual({
      id: 'p1',
      type: 'header',
      data: { text: 'Обычный <b>жирный</b> текст', level: 2 },
    })
  })
})
