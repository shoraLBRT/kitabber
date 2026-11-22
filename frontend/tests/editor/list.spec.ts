import type { BlockToolConstructorOptions, HTMLPasteEvent } from '@editorjs/editorjs'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { caretOffsetIn, placeCaret } from '../../editor/caret'
import { normalizeLevels } from '../../editor/document'
import { itemPlaceholder, listFromHtml, ListTool, styleTitles, type ListData } from '../../editor/list'

// The list block (E05-04, #39, ADR-0033): bulleted and numbered, one level of nesting, Enter and Tab, an empty item
// ends the list, the style switches without losing text.

interface Conversion {
  export: (data: ListData) => string
  import: (text: string) => ListData
}

function setup(data: Partial<ListData>, readOnly = false) {
  const api = {
    blocks: {
      insert: vi.fn(),
      getBlockIndex: vi.fn(() => 3),
      getCurrentBlockIndex: vi.fn(() => 3),
    },
    caret: { setToBlock: vi.fn() },
  }
  const block = { id: 'l1', dispatchChange: vi.fn() }
  const list = new ListTool({
    data,
    api,
    block,
    readOnly,
  } as unknown as BlockToolConstructorOptions<Partial<ListData>>)
  const holder = document.createElement('div')
  holder.append(list.render())
  document.body.append(holder)
  const items = () => [...holder.querySelectorAll<HTMLElement>('li')]
  const press = (index: number, key: string, options: KeyboardEventInit & { at?: number } = {}) => {
    const item = items()[index]!
    item.focus()
    placeCaret(item, options.at ?? 0)
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...options })
    item.dispatchEvent(event)
    return event
  }
  const save = () => list.save(holder.firstElementChild as HTMLElement)
  return { list, api, block, holder, items, press, save }
}

afterEach(() => {
  document.body.innerHTML = ''
})

describe('ListTool', () => {
  it('offers a bulleted and a numbered list in the block menu', () => {
    const entries = ListTool.toolbox as { title: string; data: { style: string } }[]
    expect(entries.map((entry) => entry.data.style)).toEqual(['unordered', 'ordered'])
    expect(entries.map((entry) => entry.title)).toEqual(['Bulleted list', 'Numbered list'])
  })

  it.each([
    ['unordered', 'UL'],
    ['ordered', 'OL'],
  ] as const)('renders a %s list as %s with an editable item per entry and its level', (style, tag) => {
    const { holder, items } = setup({ style, items: ['один', '<b>два</b>'], levels: [0, 1] })
    expect(holder.querySelector('ul, ol')!.tagName).toBe(tag)
    expect(items().map((item) => item.innerHTML)).toEqual(['один', '<b>два</b>'])
    expect(items().map((item) => item.dataset.level)).toEqual(['0', '1'])
    expect(items().every((item) => item.getAttribute('contenteditable') === 'true')).toBe(true)
  })

  it('starts empty with one item and its placeholder, bulleted by default', () => {
    const { holder, items, save } = setup({})
    expect(holder.querySelector('ul, ol')!.tagName).toBe('UL')
    expect(items()).toHaveLength(1)
    expect(items()[0]!.dataset.placeholder).toBe(itemPlaceholder)
    expect(save()).toEqual({ style: 'unordered', items: [''], levels: [] })
  })

  it('saves levels only when an item is nested, and fixes levels a stored list cannot have', () => {
    expect(setup({ style: 'ordered', items: ['a', 'b'], levels: [] }).save().levels).toEqual([])
    expect(setup({ style: 'ordered', items: ['a', 'b'], levels: [1, 1] }).save().levels).toEqual([0, 1])
    expect(setup({ style: 'ordered', items: ['a', 'b', 'c'], levels: [0, 1, 1] }).save().levels).toEqual([
      0, 1, 1,
    ])
  })

  it('nests an item under the one above on Tab and lifts it back on Shift+Tab', () => {
    const { press, save, block } = setup({ style: 'unordered', items: ['a', 'b', 'c'] })

    expect(press(1, 'Tab').defaultPrevented).toBe(true)
    expect(save().levels).toEqual([0, 1, 0])
    press(1, 'Tab')
    expect(save().levels).toEqual([0, 1, 0])
    press(2, 'Tab')
    expect(save().levels).toEqual([0, 1, 1])
    press(1, 'Tab', { shiftKey: true })
    expect(save().levels).toEqual([0, 0, 1])
    expect(block.dispatchChange).toHaveBeenCalled()
  })

  it('keeps the first item at the top on Tab and keeps the caret in place', () => {
    const { press, save, items } = setup({ style: 'unordered', items: ['первый', 'второй'] })
    expect(press(0, 'Tab').defaultPrevented).toBe(true)
    expect(save().levels).toEqual([])
    press(1, 'Tab', { at: 3 })
    expect(caretOffsetIn(items()[1]!, window.getSelection())).toBe(3)
  })

  it('splits the item at the caret on Enter, the new item at the same level', () => {
    const { press, save, items, api } = setup({ style: 'ordered', items: ['a', 'первый'], levels: [0, 1] })

    expect(press(1, 'Enter', { at: 3 }).defaultPrevented).toBe(true)
    expect(save()).toEqual({ style: 'ordered', items: ['a', 'пер', 'вый'], levels: [0, 1, 1] })
    expect(document.activeElement).toBe(items()[2])
    expect(api.blocks.insert).not.toHaveBeenCalled()
  })

  it('adds an empty item on Enter at the end of an item', () => {
    const { press, save } = setup({ style: 'unordered', items: ['один'] })
    press(0, 'Enter', { at: 4 })
    expect(save().items).toEqual(['один', ''])
  })

  it('lifts an empty nested item to the top on Enter', () => {
    const { press, save, api } = setup({ style: 'unordered', items: ['a', ''], levels: [0, 1] })
    press(1, 'Enter')
    expect(save()).toEqual({ style: 'unordered', items: ['a', ''], levels: [] })
    expect(api.blocks.insert).not.toHaveBeenCalled()
  })

  it('ends the list on Enter in an empty last item: the item becomes a text block after the list', () => {
    const { press, save, api } = setup({ style: 'unordered', items: ['a', 'b', ''] })
    press(2, 'Enter')
    expect(save().items).toEqual(['a', 'b'])
    expect(api.blocks.insert).toHaveBeenCalledTimes(1)
    expect(api.blocks.insert).toHaveBeenCalledWith('paragraph', { text: '' }, undefined, 4, true)
    expect(api.caret.setToBlock).toHaveBeenCalledWith(4, 'start')
  })

  it('splits the list on Enter in an empty middle item: text block, then the items below as a list', () => {
    const { press, save, api } = setup({ style: 'ordered', items: ['a', '', 'b', 'c'], levels: [0, 0, 1, 0] })
    press(1, 'Enter')
    expect(save().items).toEqual(['a'])
    expect(api.blocks.insert).toHaveBeenNthCalledWith(1, 'paragraph', { text: '' }, undefined, 4, true)
    // The first item below has no parent left, so it goes to the top.
    expect(api.blocks.insert).toHaveBeenNthCalledWith(
      2,
      'list',
      { style: 'ordered', items: ['b', 'c'], levels: [] },
      undefined,
      5,
      false,
    )
  })

  it('turns a list with only an empty item into a text block on Enter', () => {
    const { press, api } = setup({ style: 'unordered', items: [''] })
    press(0, 'Enter')
    expect(api.blocks.insert).toHaveBeenCalledWith('paragraph', { text: '' }, undefined, 3, true, true)
    expect(api.caret.setToBlock).toHaveBeenCalledWith(3, 'start')
  })

  it('lifts a nested item on Backspace at its start and joins a top item with the one above', () => {
    const { press, save, items } = setup({ style: 'unordered', items: ['ab', 'cd', 'ef'], levels: [0, 1, 0] })

    expect(press(1, 'Backspace').defaultPrevented).toBe(true)
    expect(save().levels).toEqual([])
    press(2, 'Backspace')
    expect(save().items).toEqual(['ab', 'cdef'])
    expect(caretOffsetIn(items()[1]!, window.getSelection())).toBe(2)
  })

  it('leaves Backspace at the start of the first item and inside an item to Editor.js', () => {
    const { press } = setup({ style: 'unordered', items: ['ab', 'cd'] })
    expect(press(0, 'Backspace').defaultPrevented).toBe(false)
    expect(press(1, 'Backspace', { at: 1 }).defaultPrevented).toBe(false)
  })

  it('switches the style in the block menu without losing items or levels, in the same rendered element', () => {
    const { list, holder, save, block } = setup({
      style: 'unordered',
      items: ['a', '<i>b</i>'],
      levels: [0, 1],
    })
    const menu = list.renderSettings() as { title: string; isActive: () => boolean; onActivate: () => void }[]
    expect(menu.map((entry) => entry.title)).toEqual([styleTitles.unordered, styleTitles.ordered])
    expect(menu.map((entry) => entry.isActive())).toEqual([true, false])

    menu[1]!.onActivate()

    expect(holder.querySelector('ul, ol')!.tagName).toBe('OL')
    expect(save()).toEqual({ style: 'ordered', items: ['a', '<i>b</i>'], levels: [0, 1] })
    expect(menu.map((entry) => entry.isActive())).toEqual([false, true])
    expect(block.dispatchChange).toHaveBeenCalled()
  })

  it('takes a pasted list with its kind, items and levels (E05-02, ADR-0042)', () => {
    const { list, holder, save } = setup({})
    const pasted = document.createElement('div')
    pasted.innerHTML =
      '<ol><li>a <b>b</b></li><li data-level="1">c</li><li data-level="1">d</li><li>e</li></ol>'

    list.onPaste({ detail: { data: pasted.firstElementChild } } as unknown as HTMLPasteEvent)

    expect(holder.querySelector('ul, ol')!.tagName).toBe('OL')
    expect(save()).toEqual({ style: 'ordered', items: ['a <b>b</b>', 'c', 'd', 'e'], levels: [0, 1, 1, 0] })
  })

  it('takes a pasted lone item as a bulleted list and keeps a pasted first item at the top', () => {
    const pasted = document.createElement('ul')
    pasted.innerHTML = '<li data-level="1">a</li><li data-level="1">b</li>'
    expect(listFromHtml(pasted)).toEqual({ style: 'unordered', items: ['a', 'b'], levels: [0, 1] })
    const item = document.createElement('li')
    item.innerHTML = 'x <script>1</script>'
    expect(listFromHtml(item)).toEqual({ style: 'unordered', items: ['x '], levels: [] })
  })

  it('declares the tags it takes from pasted HTML, keeping only the level of an item', () => {
    expect(ListTool.pasteConfig).toEqual({ tags: ['UL', 'OL', { LI: { 'data-level': true } }] })
  })

  it('keeps items and levels when converted to the other list style through Editor.js conversion', () => {
    const conversion = ListTool.conversionConfig as unknown as Conversion
    const text = conversion.export({ style: 'unordered', items: ['a<br>still a', 'b'], levels: [0, 1] })
    expect(text).toBe('a<br>still a<br>b')
    expect(conversion.import(text)).toEqual({
      style: 'unordered',
      items: ['a<br>still a', 'b'],
      levels: [0, 1],
    })
  })

  it('converts text to a list line by line and a list to text with a line per item', () => {
    const conversion = ListTool.conversionConfig as unknown as Conversion
    expect(conversion.import('один<br><b>два</b><br> <br>')).toEqual({
      style: 'unordered',
      items: ['один', '<b>два</b>'],
      levels: [],
    })
    expect(conversion.import('')).toEqual({ style: 'unordered', items: [''], levels: [] })
  })

  it('joins text merged into the list with its last item', () => {
    const { list, save } = setup({ style: 'unordered', items: ['a', 'b'] })
    list.merge({ items: ['c', 'd'] })
    expect(save().items).toEqual(['a', 'bc', 'd'])
  })

  it('renders read-only items without editing or key handling', () => {
    const { items, press, save } = setup({ style: 'unordered', items: ['a', 'b'] }, true)
    expect(items().every((item) => item.getAttribute('contenteditable') === 'false')).toBe(true)
    press(1, 'Tab')
    expect(save().levels).toEqual([])
  })
})

describe('normalizeLevels', () => {
  it.each([
    [[0, 1, 1, 0], 4, [0, 1, 1, 0]],
    [[1, 1], 2, [0, 1]],
    [[0, 3], 2, [0, 1]],
    [[0, -1, 'x'], 3, []],
    [[0, 1], 3, [0, 1, 0]],
    [[0, 1, 1], 2, [0, 1]],
  ])('normalizes %j for %i items to %j', (levels, count, expected) => {
    expect(normalizeLevels(levels, count)).toEqual(expected)
  })
})
