// Ctrl+B and Ctrl+I with no selection (#387): the command is given before Editor.js swallows the key.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { markAtCaret } from '../../editor/engine'

let holder: HTMLElement
let field: HTMLElement
let stop: () => void
let exec: ReturnType<typeof vi.fn>

function caretIn(node: Node, collapsed = true) {
  const range = document.createRange()
  range.selectNodeContents(node)
  if (collapsed) range.collapse(false)
  const selection = window.getSelection()!
  selection.removeAllRanges()
  selection.addRange(range)
}

function press(init: KeyboardEventInit) {
  const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init })
  const reached = vi.fn()
  document.addEventListener('keydown', reached, { once: true })
  field.dispatchEvent(event)
  document.removeEventListener('keydown', reached)
  return { prevented: event.defaultPrevented, reachedDocument: reached.mock.calls.length > 0 }
}

beforeEach(() => {
  holder = document.createElement('div')
  field = document.createElement('li')
  field.contentEditable = 'true'
  field.textContent = 'Пункт'
  holder.append(field)
  document.body.append(holder)
  exec = vi.fn(() => true)
  Object.defineProperty(document, 'execCommand', { configurable: true, value: exec })
  stop = markAtCaret(holder)
})

afterEach(() => {
  stop()
  holder.remove()
})

describe('markAtCaret', () => {
  it.each([
    ['KeyB', 'bold'],
    ['KeyI', 'italic'],
  ])('gives %s at a bare caret as the %s command and keeps it from Editor.js', (code, command) => {
    caretIn(field)
    // `и` and `ш` are the same keys in the Russian layout: the key is read by position.
    const result = press({ code, key: code === 'KeyB' ? 'и' : 'ш', ctrlKey: true })
    expect(exec).toHaveBeenCalledWith(command)
    expect(result).toEqual({ prevented: true, reachedDocument: false })
  })

  it('works with Cmd on a Mac', () => {
    caretIn(field)
    press({ code: 'KeyB', key: 'b', metaKey: true })
    expect(exec).toHaveBeenCalledWith('bold')
  })

  it('leaves a selection to Editor.js', () => {
    caretIn(field, false)
    const result = press({ code: 'KeyB', key: 'b', ctrlKey: true })
    expect(exec).not.toHaveBeenCalled()
    expect(result).toEqual({ prevented: false, reachedDocument: true })
  })

  it('leaves other keys and combinations alone', () => {
    caretIn(field)
    press({ code: 'KeyB', key: 'b' })
    press({ code: 'KeyB', key: 'B', ctrlKey: true, shiftKey: true })
    press({ code: 'KeyU', key: 'u', ctrlKey: true })
    expect(exec).not.toHaveBeenCalled()
  })

  it('stops listening once stopped', () => {
    stop()
    caretIn(field)
    press({ code: 'KeyB', key: 'b', ctrlKey: true })
    expect(exec).not.toHaveBeenCalled()
    stop = markAtCaret(holder)
  })
})
