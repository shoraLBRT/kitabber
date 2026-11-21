import type { BlockToolConstructorOptions } from '@editorjs/editorjs'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { caretOffsetIn, placeCaret } from '../../editor/caret'
import { toPostDocument } from '../../editor/document'
import { quotePlaceholders, QuoteTool, type QuoteData } from '../../editor/quote'

// The quote block (E05-05, #40): the text and the caption of its source in two fields, stored apart.

interface Conversion {
  export: (data: QuoteData) => string
  import: string
}

function setup(data: Partial<QuoteData>, readOnly = false) {
  const api = {
    blocks: {
      insert: vi.fn(),
      getBlockIndex: vi.fn(() => 2),
      getCurrentBlockIndex: vi.fn(() => 2),
    },
    caret: { setToBlock: vi.fn() },
  }
  const quote = new QuoteTool({
    data,
    api,
    block: { id: 'q1' },
    readOnly,
  } as unknown as BlockToolConstructorOptions<Partial<QuoteData>>)
  const holder = document.createElement('div')
  holder.append(quote.render())
  document.body.append(holder)
  const text = holder.querySelector<HTMLElement>('.cdx-quote__text')!
  const caption = holder.querySelector<HTMLElement>('.cdx-quote__caption')!
  const press = (field: HTMLElement, options: KeyboardEventInit = {}) => {
    placeCaret(field, 0)
    const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true, ...options })
    field.dispatchEvent(event)
    return event
  }
  const save = () => quote.save(holder.firstElementChild as HTMLElement)
  return { api, holder, text, caption, press, save }
}

afterEach(() => {
  document.body.innerHTML = ''
})

describe('QuoteTool', () => {
  it('offers «Цитата» in the block menu', () => {
    expect((QuoteTool.toolbox as { title: string }).title).toBe('Quote')
  })

  it('renders the text and the caption as two editable fields with placeholders', () => {
    const { holder, text, caption } = setup({ text: 'Слова', caption: 'Автор' })
    expect(holder.firstElementChild?.tagName).toBe('BLOCKQUOTE')
    expect(text.innerHTML).toBe('Слова')
    expect(caption.innerHTML).toBe('Автор')
    expect([text.contentEditable, caption.contentEditable]).toEqual(['true', 'true'])
    expect(text.dataset.placeholder).toBe(quotePlaceholders.text)
    expect(caption.dataset.placeholder).toBe(quotePlaceholders.caption)
  })

  it('stores the text and the caption apart, with their markup', () => {
    const { text, caption, save } = setup({})
    text.innerHTML = 'Быть или <b>не быть</b>'
    caption.innerHTML = '<i>Гамлет</i>'
    expect(save()).toEqual({ text: 'Быть или <b>не быть</b>', caption: '<i>Гамлет</i>' })
  })

  it('saves a field the browser left with only a line break as empty', () => {
    const { text, caption, save } = setup({ text: 'Слова', caption: 'Автор' })
    caption.innerHTML = '<br>'
    text.innerHTML = '&nbsp;<br>'
    expect(save()).toEqual({ text: '', caption: '' })
  })

  it('keeps the caption empty for a quote saved without one', () => {
    const { caption, save } = setup({ text: 'Слова' })
    expect(caption.innerHTML).toBe('')
    expect(save()).toEqual({ text: 'Слова', caption: '' })
  })

  it('goes from the text to the caption on Enter', () => {
    const { text, caption, press, api } = setup({ text: 'Слова' })
    const event = press(text)
    expect(event.defaultPrevented).toBe(true)
    expect(document.activeElement).toBe(caption)
    expect(caretOffsetIn(caption, window.getSelection())).toBe(0)
    expect(api.blocks.insert).not.toHaveBeenCalled()
  })

  it('leaves Shift+Enter in the text to the browser as a line break', () => {
    const { text, press } = setup({ text: 'Слова' })
    expect(press(text, { shiftKey: true }).defaultPrevented).toBe(false)
  })

  it.each([false, true])(
    'goes on with a text block after the quote on Enter in the caption (Shift %s)',
    (shiftKey) => {
      const { caption, press, api } = setup({ text: 'Слова', caption: 'Автор' })
      expect(press(caption, { shiftKey }).defaultPrevented).toBe(true)
      expect(api.blocks.insert).toHaveBeenCalledWith('paragraph', { text: '' }, undefined, 3, true)
      expect(api.caret.setToBlock).toHaveBeenCalledWith(3, 'start')
    },
  )

  it('is not editable and ignores keys in read-only mode', () => {
    const { text, caption, press, api } = setup({ text: 'Слова', caption: 'Автор' }, true)
    expect([text.contentEditable, caption.contentEditable]).toEqual(['false', 'false'])
    expect(press(caption).defaultPrevented).toBe(false)
    expect(api.blocks.insert).not.toHaveBeenCalled()
  })

  it('converts from text and to text keeping the caption after a dash', () => {
    const conversion = QuoteTool.conversionConfig as Conversion
    expect(conversion.import).toBe('text')
    expect(conversion.export({ text: 'Слова', caption: 'Автор' })).toBe('Слова — Автор')
    expect(conversion.export({ text: 'Слова', caption: '' })).toBe('Слова')
  })

  it('gives the schema form of the quote with both fields cleaned', () => {
    const { text, caption, save } = setup({})
    text.innerHTML = 'Слова <span style="color:red">цветные</span>'
    caption.innerHTML = 'Автор'
    const document = toPostDocument({ blocks: [{ id: 'q1', type: 'quote', data: save() }] })
    expect(document.blocks).toEqual([
      { id: 'q1', type: 'quote', data: { text: 'Слова цветные', caption: 'Автор' } },
    ])
  })
})
