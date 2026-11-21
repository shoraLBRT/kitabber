import type { OutputData } from '@editorjs/editorjs'
import { describe, expect, it } from 'vitest'
import {
  emptyDocument,
  isEmptyDocument,
  sameDocument,
  toEditorData,
  toPostDocument,
  UnsupportedBlockError,
  type PostDocument,
} from '../../editor/document'

// Every block type of schema version 1 (ADR-0023), with inline markup of the allow list.
const everyBlock: PostDocument = {
  version: 1,
  blocks: [
    {
      id: 'p1',
      type: 'paragraph',
      data: { text: 'Текст с <b>жирным</b>, <i>курсивом</i> и <mark>маркером</mark>' },
    },
    { id: 'h2', type: 'header', data: { text: 'Подзаголовок', level: 2 } },
    { id: 'h3', type: 'header', data: { text: 'Ещё уровень', level: 3 } },
    {
      id: 'l1',
      type: 'list',
      data: { style: 'ordered', items: ['первый', '<a href="https://example.org">второй</a>'] },
    },
    { id: 'l2', type: 'list', data: { style: 'unordered', items: ['пункт'] } },
    { id: 'q1', type: 'quote', data: { text: 'Цитата<br>в две строки', caption: 'Автор' } },
    { id: 'd1', type: 'delimiter', data: {} },
    { id: 'p2', type: 'paragraph', data: { text: '' } },
  ],
}

describe('post document', () => {
  it('survives the round trip through the Editor.js data without loss', () => {
    const restored = toPostDocument(toEditorData(everyBlock))

    expect(restored).toEqual(everyBlock)
    expect(sameDocument(restored, everyBlock)).toBe(true)
  })

  it('survives the round trip through JSON', () => {
    const restored = toPostDocument(JSON.parse(JSON.stringify(toEditorData(everyBlock))) as OutputData)

    expect(JSON.stringify(restored)).toBe(JSON.stringify(everyBlock))
  })

  it('drops the service fields of Editor.js and keeps the schema fields in schema order', () => {
    const output = {
      time: 1_700_000_000_000,
      version: '2.31.7',
      blocks: [
        { id: 'a', type: 'quote', data: { caption: 'Кто', alignment: 'left', text: 'Что' }, tunes: { x: 1 } },
        { id: 'b', type: 'header', data: { level: 3, text: 'Заголовок' } },
      ],
    } as OutputData

    expect(JSON.stringify(toPostDocument(output))).toBe(
      JSON.stringify({
        version: 1,
        blocks: [
          { id: 'a', type: 'quote', data: { text: 'Что', caption: 'Кто' } },
          { id: 'b', type: 'header', data: { text: 'Заголовок', level: 3 } },
        ],
      }),
    )
  })

  it('flattens the nested items of the Editor.js list plugin into the flat list of version 1', () => {
    const output = {
      blocks: [
        {
          id: 'l',
          type: 'list',
          data: {
            style: 'ordered',
            meta: {},
            items: [
              { content: 'один', meta: {}, items: [{ content: 'один-а', meta: {}, items: [] }] },
              { content: 'два', meta: {}, items: [] },
            ],
          },
        },
      ],
    } as OutputData

    expect(toPostDocument(output).blocks[0]).toEqual({
      id: 'l',
      type: 'list',
      data: { style: 'ordered', items: ['один', 'один-а', 'два'] },
    })
  })

  it('fills a missing quote caption with the schema default', () => {
    const output = { blocks: [{ id: 'q', type: 'quote', data: { text: 'Слова' } }] } as OutputData

    expect(toPostDocument(output).blocks[0]).toEqual({
      id: 'q',
      type: 'quote',
      data: { text: 'Слова', caption: '' },
    })
  })

  it('rejects a block type the schema does not have', () => {
    const output = { blocks: [{ id: 'x', type: 'poll', data: {} }] } as OutputData

    expect(() => toPostDocument(output)).toThrow(UnsupportedBlockError)
  })

  it('does not share data with the document it was made from', () => {
    const data = toEditorData(everyBlock)
    ;(data.blocks[0]!.data as { text: string }).text = 'изменено'

    expect(everyBlock.blocks[0]!.data).toEqual({
      text: 'Текст с <b>жирным</b>, <i>курсивом</i> и <mark>маркером</mark>',
    })
  })

  it('counts a document as empty while no block has visible text', () => {
    expect(isEmptyDocument(emptyDocument())).toBe(true)
    expect(
      isEmptyDocument({
        version: 1,
        blocks: [
          { id: 'a', type: 'paragraph', data: { text: '<b> </b>&nbsp;<br>' } },
          { id: 'b', type: 'delimiter', data: {} },
        ],
      }),
    ).toBe(true)
    expect(isEmptyDocument(everyBlock)).toBe(false)
  })
})
