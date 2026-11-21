import { describe, expect, it } from 'vitest'
import type { PostDocument } from '../../editor/document'
import { EditorHistory } from '../../editor/history'

const doc = (...texts: string[]): PostDocument => ({
  version: 1,
  blocks: texts.map((text, i) => ({ id: `b${i}`, type: 'paragraph', data: { text } })),
})

function clock() {
  let now = 0
  return { now: () => now, advance: (ms: number) => (now += ms) }
}

describe('editor history', () => {
  it('undoes and redoes across blocks: text, a new block, text in the new block', () => {
    const time = clock()
    const history = new EditorHistory({ document: doc(''), caret: null }, { now: time.now })

    history.record({ document: doc('Первый'), caret: { block: 0, offset: 6 } })
    time.advance(2000)
    history.record({ document: doc('Первый', ''), caret: { block: 1, offset: 0 } })
    time.advance(100)
    history.record({ document: doc('Первый', 'Второй'), caret: { block: 1, offset: 6 } })

    expect(history.undo()).toEqual({ document: doc('Первый', ''), caret: { block: 1, offset: 0 } })
    expect(history.undo()).toEqual({ document: doc('Первый'), caret: { block: 0, offset: 6 } })
    expect(history.undo()).toEqual({ document: doc(''), caret: null })
    expect(history.undo()).toBeNull()
    expect(history.canUndo).toBe(false)

    expect(history.redo()?.document).toEqual(doc('Первый'))
    expect(history.redo()?.document).toEqual(doc('Первый', ''))
    expect(history.redo()?.document).toEqual(doc('Первый', 'Второй'))
    expect(history.redo()).toBeNull()
    expect(history.canRedo).toBe(false)
  })

  it('merges typing in one block without a pause into one step', () => {
    const time = clock()
    const history = new EditorHistory({ document: doc('', 'x'), caret: null }, { now: time.now })

    history.record({ document: doc('', 'x'), caret: null })
    history.record({ document: doc('а', 'x'), caret: null })
    time.advance(300)
    history.record({ document: doc('аб', 'x'), caret: null })
    time.advance(300)
    history.record({ document: doc('абв', 'x'), caret: null })

    expect(history.undo()?.document).toEqual(doc('', 'x'))
    expect(history.canUndo).toBe(false)
  })

  it('starts a new step after a pause, in another block, and after undo', () => {
    const time = clock()
    const history = new EditorHistory({ document: doc('', ''), caret: null }, { now: time.now })

    history.record({ document: doc('а', ''), caret: null })
    time.advance(100)
    history.record({ document: doc('аб', ''), caret: null })
    time.advance(1500)
    history.record({ document: doc('абв', ''), caret: null })
    time.advance(100)
    history.record({ document: doc('абв', 'г'), caret: null })

    expect(history.undo()?.document).toEqual(doc('абв', ''))
    time.advance(100)
    history.record({ document: doc('абв', 'д'), caret: null })
    expect(history.undo()?.document).toEqual(doc('абв', ''))
    expect(history.undo()?.document).toEqual(doc('аб', ''))
  })

  it('drops the redo steps once a new change is made', () => {
    const history = new EditorHistory({ document: doc('а'), caret: null })
    history.record({ document: doc('а', 'б'), caret: null })
    history.undo()

    history.record({ document: doc('в'), caret: null })

    expect(history.canRedo).toBe(false)
    expect(history.undo()?.document).toEqual(doc('а'))
  })

  it('ignores a record that does not change the document, such as the echo of an undo', () => {
    const history = new EditorHistory({ document: doc('а'), caret: null })
    history.record({ document: doc('а', 'б'), caret: null })
    history.undo()

    expect(history.record({ document: doc('а'), caret: { block: 0, offset: 1 } })).toBe(false)
    expect(history.canRedo).toBe(true)
  })

  it('keeps at most the step limit, dropping the oldest', () => {
    const time = clock()
    const history = new EditorHistory({ document: doc(), caret: null }, { now: time.now, limit: 3 })
    for (let n = 1; n <= 5; n++) history.record({ document: doc(...Array<string>(n).fill('x')), caret: null })

    expect(history.undo()?.document.blocks).toHaveLength(4)
    expect(history.undo()?.document.blocks).toHaveLength(3)
    expect(history.undo()).toBeNull()
  })

  it('does not let a caller change a stored step', () => {
    const history = new EditorHistory({ document: doc('а'), caret: null })
    const snapshot = { document: doc('а', 'б'), caret: null }
    history.record(snapshot)
    snapshot.document.blocks.pop()

    expect(history.current.document).toEqual(doc('а', 'б'))
  })
})
