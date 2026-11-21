import type { BlockAPI, BlockToolConstructorOptions } from '@editorjs/editorjs'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  type BlockApi,
  continueAfterDelimiter,
  delimiterLabel,
  DelimiterTool,
  removeSelectedDelimiter,
} from '../../editor/delimiter'

// The delimiter block (E05-05, #40): no content, not editable, removed by one key.

function fakeApi(count: number, index = 1) {
  const api = {
    blocks: {
      getBlockIndex: vi.fn(() => index),
      getBlocksCount: vi.fn(() => count),
      delete: vi.fn(() => (count -= 1)),
      insert: vi.fn(() => (count += 1)),
    },
    caret: { setToBlock: vi.fn() },
  }
  return api as unknown as typeof api & BlockApi
}

/** An editor holder with blocks; the one at `selected` is a delimiter and selected. */
function holderWith(selected: boolean) {
  const holder = document.createElement('div')
  holder.innerHTML = `
    <div class="ce-block" data-id="p1"><div contenteditable="true">Текст</div></div>
    <div class="ce-block${selected ? ' ce-block--selected' : ''}" data-id="d1"><div class="ce-delimiter"></div></div>
    <div class="ce-block" data-id="p2"><div contenteditable="true">Ещё</div></div>`
  document.body.append(holder)
  return holder
}

function keydown(key: string, options: KeyboardEventInit = {}) {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...options })
  document.body.dispatchEvent(event)
  return event
}

const stops: (() => void)[] = []

afterEach(() => {
  stops.splice(0).forEach((stop) => stop())
  document.body.innerHTML = ''
})

describe('DelimiterTool', () => {
  it('offers «Разделитель» in the block menu', () => {
    expect((DelimiterTool.toolbox as { title: string }).title).toBe('Delimiter')
  })

  it('renders an empty, not editable separator and saves no data', () => {
    const tool = new DelimiterTool({
      api: fakeApi(3),
      block: { id: 'd1' },
      readOnly: false,
    } as unknown as BlockToolConstructorOptions)
    const element = tool.render()
    expect(element.contentEditable).toBe('false')
    expect(element.querySelector('[contenteditable="true"]')).toBeNull()
    expect(element.innerHTML).toBe('')
    expect(element.getAttribute('role')).toBe('separator')
    expect(element.getAttribute('aria-label')).toBe(delimiterLabel)
    expect(tool.save()).toEqual({})
  })

  it('selects its block on a click', () => {
    const api = fakeApi(3)
    const tool = new DelimiterTool({
      api,
      block: { id: 'd1' },
      readOnly: false,
    } as unknown as BlockToolConstructorOptions)
    tool.render().click()
    expect(api.caret.setToBlock).toHaveBeenCalledWith(1)
  })
})

describe('removeSelectedDelimiter', () => {
  it('removes a selected delimiter on Backspace and puts the caret at the end of the block before', () => {
    const api = fakeApi(3)
    stops.push(removeSelectedDelimiter(holderWith(true), api))
    const event = keydown('Backspace')
    expect(event.defaultPrevented).toBe(true)
    expect(api.blocks.delete).toHaveBeenCalledWith(1)
    expect(api.caret.setToBlock).toHaveBeenCalledWith(0, 'end')
  })

  it('removes a selected delimiter on Delete and puts the caret at the start of the block after', () => {
    const api = fakeApi(3)
    stops.push(removeSelectedDelimiter(holderWith(true), api))
    keydown('Delete')
    expect(api.blocks.delete).toHaveBeenCalledWith(1)
    expect(api.caret.setToBlock).toHaveBeenCalledWith(1, 'start')
  })

  it('puts the caret after the first delimiter removed on Backspace', () => {
    const api = fakeApi(3, 0)
    stops.push(removeSelectedDelimiter(holderWith(true), api))
    keydown('Backspace')
    expect(api.caret.setToBlock).toHaveBeenCalledWith(0, 'start')
  })

  it('puts the caret before the last delimiter removed on Delete', () => {
    const api = fakeApi(3, 2)
    stops.push(removeSelectedDelimiter(holderWith(true), api))
    keydown('Delete')
    expect(api.caret.setToBlock).toHaveBeenCalledWith(1, 'end')
  })

  it('leaves keys alone when no delimiter is selected or a modifier is held', () => {
    const api = fakeApi(3)
    stops.push(removeSelectedDelimiter(holderWith(false), api))
    expect(keydown('Backspace').defaultPrevented).toBe(false)
    document.querySelector('[data-id="d1"]')!.classList.add('ce-block--selected')
    expect(keydown('Backspace', { ctrlKey: true }).defaultPrevented).toBe(false)
    expect(keydown('a').defaultPrevented).toBe(false)
    expect(api.blocks.delete).not.toHaveBeenCalled()
  })

  it('stops listening when stopped', () => {
    const api = fakeApi(3)
    removeSelectedDelimiter(holderWith(true), api)()
    keydown('Backspace')
    expect(api.blocks.delete).not.toHaveBeenCalled()
  })
})

describe('continueAfterDelimiter', () => {
  const added = (name: string, selected = true) =>
    ({ id: 'd1', name, selected }) as unknown as BlockAPI

  it('adds a text block after a delimiter added last and puts the caret there', () => {
    const api = fakeApi(2)
    continueAfterDelimiter(api, added('delimiter'))
    expect(api.blocks.insert).toHaveBeenCalledWith('paragraph', { text: '' }, undefined, 2, true)
    expect(api.caret.setToBlock).toHaveBeenCalledWith(2, 'start')
  })

  it('puts the caret on the block after a delimiter added in the middle', () => {
    const api = fakeApi(3)
    continueAfterDelimiter(api, added('delimiter'))
    expect(api.blocks.insert).not.toHaveBeenCalled()
    expect(api.caret.setToBlock).toHaveBeenCalledWith(2, 'start')
  })

  it('leaves the caret alone once the author has moved it off the new delimiter', () => {
    const api = fakeApi(2)
    continueAfterDelimiter(api, added('delimiter', false))
    expect(api.blocks.insert).not.toHaveBeenCalled()
    expect(api.caret.setToBlock).not.toHaveBeenCalled()
  })

  it('ignores other blocks', () => {
    const api = fakeApi(2)
    continueAfterDelimiter(api, added('paragraph'))
    expect(api.blocks.insert).not.toHaveBeenCalled()
    expect(api.caret.setToBlock).not.toHaveBeenCalled()
  })
})
