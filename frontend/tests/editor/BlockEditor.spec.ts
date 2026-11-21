import type { OutputData } from '@editorjs/editorjs'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import BlockEditor from '../../components/editor/BlockEditor.vue'
import { emptyDocument, type PostDocument } from '../../editor/document'
import type { CreateEngine, EngineOptions } from '../../editor/engine'
import type { Caret } from '../../editor/history'

// A fake engine in place of Editor.js (docs/TESTING.md): the test edits its content and reports changes as
// Editor.js would; render() is what undo and redo call.
class FakeEngine {
  data: OutputData
  caretAt: Caret | null = null
  renders: OutputData[] = []
  destroyed = false
  constructor(readonly options: EngineOptions) {
    this.data = structuredClone(options.data)
  }
  type(...texts: string[]) {
    this.data = { blocks: texts.map((text, i) => ({ id: `b${i}`, type: 'paragraph', data: { text } })) }
    this.caretAt = { block: texts.length - 1, offset: texts.at(-1)!.length }
    this.options.onChange()
  }
  save = async () => structuredClone(this.data)
  render = async (next: OutputData) => {
    this.renders.push(next)
    this.data = structuredClone(next)
  }
  caret = () => this.caretAt
  setCaret = (caret: Caret) => {
    this.caretAt = caret
  }
  destroy = () => {
    this.destroyed = true
  }
}

let engine: FakeEngine
let wrapper: ReturnType<typeof mount<typeof BlockEditor>>
let time = 0

const fakeEngine: CreateEngine = async (options) => {
  engine = new FakeEngine(options)
  return engine
}

async function mountEditor(
  createEngine: CreateEngine = fakeEngine,
  document: PostDocument = emptyDocument(),
) {
  wrapper = mount(BlockEditor, {
    props: {
      document,
      createEngine,
      now: () => time,
      'onUpdate:document': (next: PostDocument) => wrapper.setProps({ document: next }),
    },
    attachTo: window.document.body,
  })
  await flushPromises()
}

const texts = () =>
  (wrapper.props('document') as PostDocument).blocks.map((block) => (block.data as { text: string }).text)

async function keydown(code: string, options: KeyboardEventInit = {}) {
  const event = new KeyboardEvent('keydown', {
    code,
    ctrlKey: true,
    bubbles: true,
    cancelable: true,
    ...options,
  })
  wrapper.get('[data-testid="block-editor-holder"]').element.dispatchEvent(event)
  await flushPromises()
  return event
}

afterEach(() => {
  if (!engine?.destroyed) wrapper?.unmount()
  time = 0
})

describe('BlockEditor', () => {
  it('shows skeleton lines while the engine starts, then the editor', async () => {
    let resolve!: () => void
    const slow: CreateEngine = (options) =>
      new Promise((done) => {
        resolve = () => done(new FakeEngine(options))
      })
    wrapper = mount(BlockEditor, { props: { document: emptyDocument(), createEngine: slow } })
    await flushPromises()

    expect(wrapper.attributes('data-state')).toBe('loading')
    expect(wrapper.find('[role="status"]').text()).toBe('Редактор загружается…')

    resolve()
    await flushPromises()

    expect(wrapper.attributes('data-state')).toBe('ready')
    expect(wrapper.find('[role="status"]').exists()).toBe(false)
  })

  it('starts empty with the placeholder and nothing to undo', async () => {
    await mountEditor()

    expect(engine.options.placeholder).toBe('Начните писать…')
    expect(engine.options.data).toEqual({ blocks: [] })
    expect(wrapper.vm.canUndo).toBe(false)
    expect(wrapper.vm.canRedo).toBe(false)
  })

  it('shows an error with "Повторить" when the engine fails to start, and starts again on it', async () => {
    let fail = true
    await mountEditor(async (options) => {
      if (fail) throw new Error('chunk failed to load')
      return fakeEngine(options)
    })

    expect(wrapper.attributes('data-state')).toBe('error')
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось открыть редактор')

    fail = false
    await wrapper.get('[role="alert"] button').trigger('click')
    await flushPromises()

    expect(wrapper.attributes('data-state')).toBe('ready')
  })

  it('reports the edited document in the schema format', async () => {
    await mountEditor()

    engine.type('Привет')
    await flushPromises()

    expect(wrapper.props('document')).toEqual({
      version: 1,
      blocks: [{ id: 'b0', type: 'paragraph', data: { text: 'Привет' } }],
    })
  })

  it('opens a saved document and gives it to the engine', async () => {
    const saved: PostDocument = {
      version: 1,
      blocks: [{ id: 'x1', type: 'header', data: { text: 'Заголовок', level: 2 } }],
    }
    await mountEditor(fakeEngine, saved)

    expect(engine.options.data).toEqual({
      blocks: [{ id: 'x1', type: 'header', data: { text: 'Заголовок', level: 2 } }],
    })
  })

  it('undoes and redoes across blocks with Ctrl+Z and Ctrl+Shift+Z, putting the caret back', async () => {
    await mountEditor()
    engine.type('Первый')
    await flushPromises()
    time += 2000
    engine.type('Первый', '')
    await flushPromises()
    engine.type('Первый', 'Второй')
    await flushPromises()

    const undo = await keydown('KeyZ')
    expect(undo.defaultPrevented).toBe(true)
    expect(texts()).toEqual(['Первый', ''])
    await keydown('KeyZ')
    expect(texts()).toEqual(['Первый'])
    expect(engine.caretAt).toEqual({ block: 0, offset: 6 })
    expect(engine.renders.at(-1)).toEqual({
      blocks: [{ id: 'b0', type: 'paragraph', data: { text: 'Первый' } }],
    })

    await keydown('KeyZ', { shiftKey: true })
    expect(texts()).toEqual(['Первый', ''])
    await keydown('KeyY')
    expect(texts()).toEqual(['Первый', 'Второй'])
    expect(wrapper.vm.canRedo).toBe(false)
  })

  it('reads the undo keys by position, so Cmd+Z and the Russian layout work', async () => {
    await mountEditor()
    engine.type('Текст')
    await flushPromises()

    await keydown('KeyZ', { key: 'я', ctrlKey: false, metaKey: true })

    expect(texts()).toEqual([])
  })

  it('takes Ctrl+Z pressed while the focus is nowhere (blocks being re-rendered), not in another field', async () => {
    await mountEditor()
    engine.type('Текст')
    await flushPromises()
    const field = window.document.createElement('input')
    window.document.body.append(field)

    const elsewhere = new KeyboardEvent('keydown', {
      code: 'KeyZ',
      ctrlKey: true,
      bubbles: true,
      cancelable: true,
    })
    field.dispatchEvent(elsewhere)
    await flushPromises()
    expect(elsewhere.defaultPrevented).toBe(false)
    expect(texts()).toEqual(['Текст'])

    window.document.body.dispatchEvent(
      new KeyboardEvent('keydown', { code: 'KeyZ', ctrlKey: true, bubbles: true, cancelable: true }),
    )
    await flushPromises()
    expect(texts()).toEqual([])
    field.remove()
  })

  it('leaves other shortcuts alone', async () => {
    await mountEditor()
    engine.type('Текст')
    await flushPromises()

    const bold = await keydown('KeyB')
    const plainZ = await keydown('KeyZ', { ctrlKey: false })

    expect(bold.defaultPrevented).toBe(false)
    expect(plainZ.defaultPrevented).toBe(false)
    expect(texts()).toEqual(['Текст'])
  })

  it('records a change the engine has not reported yet before undoing, so it is not lost', async () => {
    await mountEditor()
    engine.type('Первый')
    await flushPromises()
    time += 2000
    // Typed, but Editor.js has not called onChange yet.
    engine.data = { blocks: [{ id: 'b0', type: 'paragraph', data: { text: 'Первый и ещё' } }] }

    await wrapper.vm.undo()
    expect(texts()).toEqual(['Первый'])
    await wrapper.vm.redo()
    expect(texts()).toEqual(['Первый и ещё'])
  })

  it('does not record the echo of an undo as a new step', async () => {
    await mountEditor()
    engine.type('а')
    await flushPromises()
    time += 2000
    engine.type('а', 'б')
    await flushPromises()

    await wrapper.vm.undo()
    engine.options.onChange()
    await flushPromises()

    expect(wrapper.vm.canRedo).toBe(true)
  })

  it('destroys the engine when it goes away', async () => {
    await mountEditor()
    wrapper.unmount()

    expect(engine.destroyed).toBe(true)
  })
})
