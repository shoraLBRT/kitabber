import type { OutputData } from '@editorjs/editorjs'
import { DOMWrapper, flushPromises, mount } from '@vue/test-utils'
import { reactive } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PostEditor from '../../components/editor/PostEditor.vue'
import type { PublishInput, PublishOutcome } from '../../api/publish'
import type { CreateEngine, EngineOptions } from '../../editor/engine'
import { postTitleMaxLength } from '../../editor/publish'
import type { QuickPostHandoff } from '../../editor/quick-post'
import { useToast } from '../../composables/useToast'

// Publishing from the editor (E05-09, docs/ui/editor.md, ADR-0046): «Опубликовать» waits for text, the title is
// optional and its length is checked before anything is sent, the topic goes as the blog, the server's field errors
// are shown in place and any other refusal is a toast with the window still open. Editor.js is a fake (docs/TESTING.md).
class FakeEngine {
  data: OutputData
  constructor(readonly options: EngineOptions) {
    this.data = structuredClone(options.data)
  }
  type(...texts: string[]) {
    this.data = { blocks: texts.map((text, i) => ({ id: `b${i}`, type: 'paragraph', data: { text } })) }
    this.options.onChange()
  }
  save = async () => structuredClone(this.data)
  render = async (next: OutputData) => {
    this.data = structuredClone(next)
  }
  caret = () => null
  setCaret = () => undefined
  menus = 0
  formats: string[] = []
  openBlockMenu = () => {
    this.menus++
  }
  format = (mark: string) => {
    this.formats.push(mark)
  }
  images: File[] = []
  addImage = (file: File) => {
    this.images.push(file)
  }
  destroy = () => undefined
}

const topics = [
  { id: '0195f3a0-0000-7000-8000-0000000000t1', name: 'Игры', nickname: 'games' },
  { id: '0195f3a0-0000-7000-8000-0000000000t2', name: 'Книги', nickname: 'books' },
]

let engine: FakeEngine
let wrapper: ReturnType<typeof mount<typeof PostEditor>>

const createEngine: CreateEngine = async (options) => {
  engine = new FakeEngine(options)
  return engine
}

// The window teleports to #teleports, as on the site; the stub of Teleport re-renders the window without end.
async function mountEditor(
  publish: (input: PublishInput) => Promise<PublishOutcome>,
  initial: QuickPostHandoff | null = null,
) {
  document.body.innerHTML = '<div id="teleports"></div><div id="app"></div>'
  wrapper = mount(PostEditor, {
    props: { topics, publish, createEngine, initial },
    attachTo: '#app',
  })
  await flushPromises()
}

const page = () => new DOMWrapper(document.body)
const button = () => page().get('[data-testid="editor-publish"]')

async function type(...texts: string[]) {
  engine.type(...texts)
  await flushPromises()
}

async function clickPublish() {
  await button().trigger('click')
  await flushPromises()
}

const published = (path = '/u/anna/0195f3a0-0000-7000-8000-0000000000c9'): PublishOutcome => ({
  status: 'published',
  path,
  post: {} as never,
})

afterEach(() => {
  wrapper?.unmount()
  useToast().clear()
})

describe('PostEditor', () => {
  it('gives a phone the panel above the keyboard, wired to the blocks (E05-10)', async () => {
    vi.spyOn(window, 'matchMedia').mockImplementation(
      (query) =>
        ({
          matches: true,
          media: query,
          addEventListener: () => {},
          removeEventListener: () => {},
        }) as unknown as MediaQueryList,
    )
    await mountEditor(vi.fn(async () => published()))

    const bar = page().get('[data-testid="editor-keyboard-bar"]')
    // The bar is the row between the text and «Опубликовать», the row the window keeps above the keyboard.
    expect(bar.element.nextElementSibling?.tagName).toBe('FOOTER')
    await page().get('[data-testid="keyboard-bar-add"]').trigger('click')
    await page().get('[data-testid="keyboard-bar-bold"]').trigger('click')
    await page().get('[data-testid="keyboard-bar-italic"]').trigger('click')
    expect(engine.menus).toBe(1)
    expect(engine.formats).toEqual(['bold', 'italic'])
    vi.restoreAllMocks()
  })

  it('starts from what the quick post handed over: its blocks and its topic (E05-29)', async () => {
    const blocks = [
      { id: 'q1', type: 'paragraph' as const, data: { text: 'Из быстрой формы' } },
      { id: 'q2', type: 'image' as const, data: { media: 'm1', width: 40, height: 30, caption: '' } },
    ]
    const publish = vi.fn(async (_: PublishInput) => published())
    // The hand-off comes from the page's reactive state (useState), as on the site.
    await mountEditor(publish, reactive({ document: { version: 1, blocks }, topicId: topics[1]!.id }))

    expect(engine.options.data.blocks).toEqual(blocks)
    expect(page().get('[data-testid="editor-topic"]').text()).toContain('Книги')
    expect(button().attributes('disabled')).toBeUndefined()
    await clickPublish()
    expect(publish.mock.calls[0]![0]).toMatchObject({ blogId: topics[1]!.id, body: { version: 1, blocks } })
  })

  it('cannot publish an empty post: the button waits for a block with text', async () => {
    const publish = vi.fn(async () => published())
    await mountEditor(publish)

    expect(button().attributes('disabled')).toBeDefined()
    await type('   ', '<b> </b>')
    expect(button().attributes('disabled')).toBeDefined()

    await type('Первый абзац')
    expect(button().attributes('disabled')).toBeUndefined()
  })

  it('publishes a short post without a title and without a topic, then hands over its address', async () => {
    const publish = vi.fn(async () => published())
    await mountEditor(publish)

    await type('Короткий пост')
    await clickPublish()

    expect(publish).toHaveBeenCalledWith({
      draftId: null,
      title: null,
      blogId: null,
      body: { version: 1, blocks: [{ id: 'b0', type: 'paragraph', data: { text: 'Короткий пост' } }] },
    })
    expect(wrapper.emitted('published')).toEqual([['/u/anna/0195f3a0-0000-7000-8000-0000000000c9']])
  })

  it('sends the trimmed title and the chosen topic', async () => {
    const publish = vi.fn(async () => published())
    await mountEditor(publish)
    expect(page().get('[data-testid="editor-topic"] button').attributes('aria-label')).toBe('Тема: Без темы')

    await page().get('[data-testid="editor-topic"] button').trigger('click')
    await page().get('[role="menuitem"][data-id="0195f3a0-0000-7000-8000-0000000000t2"]').trigger('click')
    await page().get('[data-testid="editor-title"]').setValue('  Книги осени  ')
    await type('Что почитать')
    await clickPublish()

    expect(page().get('[data-testid="editor-topic"] button').attributes('aria-label')).toBe('Тема: Книги')
    expect(publish).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Книги осени', blogId: '0195f3a0-0000-7000-8000-0000000000t2' }),
    )
  })

  it('checks the length of the title before sending and shows the error under it', async () => {
    const publish = vi.fn(async () => published())
    await mountEditor(publish)

    await page()
      .get('[data-testid="editor-title"]')
      .setValue('я'.repeat(postTitleMaxLength + 1))
    await type('Текст')
    await clickPublish()

    expect(publish).not.toHaveBeenCalled()
    expect(page().get('[data-testid="editor-title-error"]').text()).toBe('Заголовок длиннее 120 символов.')
    expect(page().get('[data-testid="editor-title"]').attributes('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(page().get('[data-testid="editor-title"]').element)
  })

  it('shows the loading state while publishing and sends once', async () => {
    let finish!: (outcome: PublishOutcome) => void
    const publish = vi.fn(() => new Promise<PublishOutcome>((done) => (finish = done)))
    await mountEditor(publish)

    await type('Текст')
    await clickPublish()
    await clickPublish()

    expect(button().attributes('aria-busy')).toBe('true')
    expect(publish).toHaveBeenCalledTimes(1)
    finish(published())
    await flushPromises()
    expect(button().attributes('aria-busy')).toBeUndefined()
  })

  it('shows the server field errors in place', async () => {
    const publish = vi.fn(async (): Promise<PublishOutcome> => ({
      status: 'invalid',
      errors: { 'body.blocks[0]': 'Блок больше 64 КБ.' },
      draftId: 'draft-1',
    }))
    await mountEditor(publish)

    await type('Текст')
    await clickPublish()

    const error = page().get('[data-testid="editor-body-error"]')
    expect(error.text()).toBe('Блок больше 64 КБ.')
    expect(document.activeElement).toBe(error.element)
    expect(wrapper.emitted('published')).toBeUndefined()
  })

  it('tells why the server refused in a toast, keeps the window and retries the same draft', async () => {
    const publish = vi
      .fn<(input: PublishInput) => Promise<PublishOutcome>>()
      .mockResolvedValueOnce({ status: 'email-not-confirmed', draftId: 'draft-1' })
      .mockResolvedValueOnce({ status: 'error', draftId: 'draft-1' })
      .mockResolvedValueOnce(published())
    await mountEditor(publish)
    const toasts = useToast().toasts

    await type('Текст')
    await clickPublish()
    expect(toasts.at(-1)).toMatchObject({
      tone: 'error',
      message: 'Чтобы публиковать, подтвердите почту в настройках аккаунта.',
    })

    await clickPublish()
    expect(toasts.at(-1)?.message).toBe('Не удалось опубликовать пост. Попробуйте ещё раз.')
    expect(wrapper.emitted('close')).toBeUndefined()

    await clickPublish()
    expect(publish.mock.calls.map(([input]) => input.draftId)).toEqual([null, 'draft-1', 'draft-1'])
    expect(wrapper.emitted('published')).toHaveLength(1)
  })

  it('asks a guest to sign in', async () => {
    await mountEditor(async () => ({ status: 'unauthorized' }))

    await type('Текст')
    await clickPublish()

    expect(wrapper.emitted('unauthorized')).toHaveLength(1)
  })

  it('publishes on Ctrl+Enter', async () => {
    const publish = vi.fn(async () => published())
    await mountEditor(publish)

    await type('Текст')
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true, bubbles: true }))
    await flushPromises()

    expect(publish).toHaveBeenCalledTimes(1)
  })
})
