import { afterEach, describe, expect, it } from 'vitest'
import { caretOffsetIn, placeCaret } from '../../editor/caret'

function editable(html: string): HTMLElement {
  const element = document.createElement('div')
  element.contentEditable = 'true'
  element.innerHTML = html
  document.body.append(element)
  return element
}

afterEach(() => {
  document.body.replaceChildren()
})

describe('caret offset', () => {
  it('puts the caret at a text offset across markup and reads the same offset back', () => {
    const element = editable('Один <b>жирный</b> и <i>курсив</i>')

    placeCaret(element, 8)

    expect(caretOffsetIn(element, window.getSelection())).toBe(8)
    expect(document.activeElement).toBe(element)
  })

  it('clamps an offset past the end to the end of the text', () => {
    const element = editable('Коротко')

    placeCaret(element, 100)

    expect(caretOffsetIn(element, window.getSelection())).toBe(7)
  })

  it('places the caret in an empty block', () => {
    const element = editable('')

    placeCaret(element, 3)

    expect(caretOffsetIn(element, window.getSelection())).toBe(0)
  })

  it('reads nothing when the caret is outside the block', () => {
    const element = editable('Текст')
    const other = editable('Другой')
    placeCaret(other, 2)

    expect(caretOffsetIn(element, window.getSelection())).toBeNull()
    expect(caretOffsetIn(element, null)).toBeNull()
  })
})
