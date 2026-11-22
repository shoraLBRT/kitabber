import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import EditorKeyboardBar, { type KeyboardBarTarget } from '../../components/editor/EditorKeyboardBar.vue'
import EditorWindow from '../../components/editor/EditorWindow.vue'

// The editor on a phone (E05-10, ADR-0047): the screen takes the box of the visual viewport, so the on-screen keyboard
// does not cover the panel above it, and the panel's tools keep the focus in the text. The keyboard is a fake visual
// viewport (jsdom has none) that shrinks as the keyboard of iOS Safari and Android Chrome does.
class FakeViewport extends EventTarget {
  offsetTop = 0
  scale = 1
  constructor(public height: number) {
    super()
  }
  set(height: number, offsetTop = 0, scale = 1) {
    this.height = height
    this.offsetTop = offsetTop
    this.scale = scale
    this.dispatchEvent(new Event('resize'))
  }
}

const phone = (matches: boolean) =>
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query) =>
      ({
        matches,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  )

let viewport: FakeViewport

function screen(height = 844) {
  vi.stubGlobal('innerHeight', height)
  viewport = new FakeViewport(height)
  vi.stubGlobal('visualViewport', viewport)
}

let windowWrapper: ReturnType<typeof mount<typeof EditorWindow>> | undefined
let barWrapper: ReturnType<typeof mount<typeof EditorKeyboardBar>> | undefined

function mountWindow() {
  windowWrapper = mount(EditorWindow, {
    props: { title: 'Новый пост' },
    slots: {
      default: '<p contenteditable="true" data-testid="text">Текст</p>',
      toolbar: '<div data-testid="toolbar">панель</div>',
      footer: '<button type="button" data-testid="publish">Опубликовать</button>',
    },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
}

const editorWindow = () => windowWrapper!.get('[data-testid="editor-window"]')

function target(state: KeyboardBarTarget['state'] = 'ready') {
  return { state, keepSelection: vi.fn(), openBlockMenu: vi.fn(), format: vi.fn(), addImage: vi.fn() }
}

function mountBar(editor: KeyboardBarTarget) {
  barWrapper = mount(EditorKeyboardBar, { props: { editor }, attachTo: document.body })
}

afterEach(() => {
  windowWrapper?.unmount()
  barWrapper?.unmount()
  windowWrapper = undefined
  barWrapper = undefined
  document.body.innerHTML = ''
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('EditorWindow on a phone', () => {
  it('fills the screen while there is no keyboard and keeps the footer', async () => {
    phone(true)
    screen()
    mountWindow()
    await nextTick()

    expect(editorWindow().attributes('data-keyboard')).toBe('closed')
    expect(editorWindow().attributes('style')).toContain('height: 844px')
    expect(editorWindow().attributes('style')).toContain('top: 0px')
    expect(windowWrapper!.find('[data-testid="publish"]').exists()).toBe(true)
  })

  it('shrinks to what the keyboard leaves, with the toolbar as its bottom row', async () => {
    phone(true)
    screen()
    mountWindow()
    await nextTick()

    // iOS Safari: the keyboard takes 336 px and the page is scrolled by 40 px under it.
    viewport.set(508, 40)
    await nextTick()

    expect(editorWindow().attributes('data-keyboard')).toBe('open')
    expect(editorWindow().attributes('style')).toContain('height: 508px')
    expect(editorWindow().attributes('style')).toContain('top: 40px')
    expect(editorWindow().classes()).toContain('editor-window--keyboard')
    // The toolbar is the last row of the panel before the footer, which hides while the keyboard is open (styles).
    const panel = windowWrapper!.get('[role="dialog"]').element
    const toolbar = windowWrapper!.get('[data-testid="toolbar"]').element
    expect(toolbar.parentElement).toBe(panel)
    expect(toolbar.nextElementSibling?.tagName).toBe('FOOTER')

    viewport.set(844)
    await nextTick()
    expect(editorWindow().attributes('data-keyboard')).toBe('closed')
    expect(editorWindow().attributes('style')).toContain('height: 844px')
  })

  it('takes the address bar hiding for no keyboard and ignores a pinch zoom', async () => {
    phone(true)
    screen()
    mountWindow()
    await nextTick()

    viewport.set(790)
    await nextTick()
    expect(editorWindow().attributes('data-keyboard')).toBe('closed')

    viewport.set(300, 200, 2)
    await nextTick()
    expect(editorWindow().attributes('style') ?? '').not.toContain('height')
  })

  it('stays the centred window on a desktop', async () => {
    phone(false)
    screen(900)
    mountWindow()
    await nextTick()

    viewport.set(500)
    await nextTick()
    expect(editorWindow().attributes('data-keyboard')).toBe('closed')
    expect(editorWindow().attributes('style') ?? '').not.toContain('height')
  })
})

describe('EditorKeyboardBar', () => {
  it('is shown only on a phone', async () => {
    phone(false)
    mountBar(target())
    await nextTick()
    expect(barWrapper!.find('[data-testid="editor-keyboard-bar"]').exists()).toBe(false)
  })

  it('opens the block menu and formats the selection with buttons of 44 px and Russian names', async () => {
    phone(true)
    const editor = target()
    mountBar(editor)
    await nextTick()

    const bar = barWrapper!.get('[data-testid="editor-keyboard-bar"]')
    expect(bar.attributes('role')).toBe('toolbar')
    await barWrapper!.get('[data-testid="keyboard-bar-add"]').trigger('click')
    await barWrapper!.get('[data-testid="keyboard-bar-bold"]').trigger('click')
    await barWrapper!.get('[data-testid="keyboard-bar-italic"]').trigger('click')

    expect(editor.openBlockMenu).toHaveBeenCalledOnce()
    expect(editor.format.mock.calls).toEqual([['bold'], ['italic']])
    expect(bar.findAll('button').map((button) => button.attributes('aria-label'))).toEqual([
      'Добавить блок',
      'Фото из галереи',
      'Снять на камеру',
      'Жирный',
      'Курсив',
    ])
  })

  it('adds a picture from the gallery or the camera as a photo block (E05-06)', async () => {
    phone(true)
    const editor = target()
    mountBar(editor)
    await nextTick()

    const gallery = barWrapper!.get<HTMLInputElement>('[data-testid="keyboard-bar-gallery-input"]').element
    const camera = barWrapper!.get<HTMLInputElement>('[data-testid="keyboard-bar-camera-input"]').element
    expect(gallery.accept).toBe('image/*')
    expect(gallery.getAttribute('capture')).toBeNull()
    expect(camera.accept).toBe('image/*')
    expect(camera.getAttribute('capture')).toBe('environment')
    const opened = vi.spyOn(camera, 'click').mockImplementation(() => undefined)
    await barWrapper!.get('[data-testid="keyboard-bar-camera"]').trigger('click')
    expect(opened).toHaveBeenCalledOnce()

    const photo = new File(['x'], 'photo.jpg', { type: 'image/jpeg' })
    Object.defineProperty(gallery, 'files', { configurable: true, value: [photo] })
    gallery.dispatchEvent(new Event('change'))
    expect(editor.addImage).toHaveBeenCalledWith(photo)
  })

  it('keeps the focus and the caret in the text while a tool is pressed', async () => {
    phone(true)
    const text = document.createElement('p')
    text.contentEditable = 'true'
    text.textContent = 'Текст'
    document.body.append(text)
    text.focus()
    const editor = target()
    mountBar(editor)
    await nextTick()

    for (const id of ['keyboard-bar-add', 'keyboard-bar-bold', 'keyboard-bar-italic']) {
      const button = barWrapper!.get(`[data-testid="${id}"]`).element
      const pointer = new PointerEvent('pointerdown', { bubbles: true, cancelable: true })
      const mouse = new MouseEvent('mousedown', { bubbles: true, cancelable: true })
      button.dispatchEvent(pointer)
      button.dispatchEvent(mouse)
      // A cancelled pointerdown and mousedown do not move the focus to the button, so the keyboard stays open.
      expect(pointer.defaultPrevented).toBe(true)
      expect(mouse.defaultPrevented).toBe(true)
      ;(button as HTMLButtonElement).click()
      expect(document.activeElement).toBe(text)
    }
    // Each press keeps the selection first, for a browser that moves the focus anyway.
    expect(editor.keepSelection).toHaveBeenCalledTimes(3)
  })

  it('waits for the editor: the tools are off while it loads or failed', async () => {
    phone(true)
    mountBar(target('loading'))
    await nextTick()
    const buttons = barWrapper!.findAll('button')
    expect(buttons.every((button) => button.attributes('disabled') !== undefined)).toBe(true)

    await barWrapper!.setProps({ editor: target('error') })
    expect(barWrapper!.findAll('button').every((button) => button.attributes('disabled') !== undefined)).toBe(
      true,
    )
  })
})
