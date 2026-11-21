import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import EditorWindow from '../../components/editor/EditorWindow.vue'

let wrapper: ReturnType<typeof mount<typeof EditorWindow>>

const button = () => wrapper.get('[data-testid="editor-expand"]')

function mountWindow() {
  wrapper = mount(EditorWindow, {
    props: { title: 'Новый пост' },
    slots: {
      default: '<p contenteditable="true" data-testid="text">Текст</p>',
      actions: '<button type="button" class="editor-window__button" data-testid="action">Отменить</button>',
    },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
}

afterEach(() => {
  wrapper?.unmount()
})

describe('EditorWindow', () => {
  it('is a modal dialog named by its title, with the actions in the header', () => {
    mountWindow()

    const dialog = wrapper.get('[role="dialog"]')
    expect(dialog.attributes('aria-modal')).toBe('true')
    expect(wrapper.get(`#${dialog.attributes('aria-labelledby')}`).text()).toBe('Новый пост')
    expect(wrapper.find('header [data-testid="action"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="editor-close"]').attributes('aria-label')).toBe('Закрыть редактор')
  })

  it('closes on "✕", on Escape and on a click on the backdrop', async () => {
    mountWindow()

    await wrapper.get('[data-testid="editor-close"]').trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('[data-testid="editor-backdrop"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(3)
  })

  it('expands to the whole screen and back', async () => {
    mountWindow()
    const expand = { trigger: (e: string) => button().trigger(e), attributes: (a: string) => button().attributes(a) }

    await expand.trigger('click')
    expect(wrapper.get('[data-testid="editor-window"]').classes()).toContain('editor-window--expanded')
    expect(expand.attributes('aria-pressed')).toBe('true')
    expect(expand.attributes('aria-label')).toBe('Свернуть в окно')

    await expand.trigger('click')
    expect(wrapper.get('[data-testid="editor-window"]').classes()).not.toContain('editor-window--expanded')
    expect(expand.attributes('aria-label')).toBe('Развернуть на весь экран')
  })

  it('stops the page behind from scrolling while open', () => {
    mountWindow()
    expect(document.body.style.overflow).toBe('hidden')

    wrapper.unmount()
    expect(document.body.style.overflow).toBe('')
    mountWindow()
  })
})
