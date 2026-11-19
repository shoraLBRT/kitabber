import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { markRaw, type Component } from 'vue'
import UiModal from '../../components/ui/UiModal.vue'
import UiSheet from '../../components/ui/UiSheet.vue'
import DialogPage from './fixtures/DialogPage.vue'

let wrapper: ReturnType<typeof mount>
// The teleport is rendered in place so the dialog is part of the wrapper.
const mountPage = async (dialog: Component = UiModal) => {
  wrapper = mount(DialogPage, {
    props: { dialog: markRaw(dialog) },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
  const opener = wrapper.get('#opener').element as HTMLElement
  opener.focus()
  await wrapper.get('#opener').trigger('click')
  await flushPromises()
}
const dialog = () => wrapper.find('[role="dialog"]')
const key = async (key: string, shiftKey = false) => {
  document.activeElement!.dispatchEvent(
    new KeyboardEvent('keydown', { key, shiftKey, bubbles: true, cancelable: true }),
  )
  await flushPromises()
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

afterEach(() => {
  wrapper?.unmount()
  vi.restoreAllMocks()
})

describe('UiModal', () => {
  it('is a labelled modal dialog that takes the focus', async () => {
    await mountPage()
    expect(dialog().attributes('aria-modal')).toBe('true')
    const title = wrapper.get(`[id="${dialog().attributes('aria-labelledby')}"]`)
    expect(title.text()).toBe('Удалить черновик?')
    expect(dialog().element.contains(document.activeElement)).toBe(true)
    expect(document.body.style.overflow).toBe('hidden')
  })

  it('keeps Tab and Shift+Tab inside the dialog', async () => {
    await mountPage()
    const close = wrapper.get('.ui-dialog__close').element
    const ok = wrapper.get('#ok').element as HTMLElement
    ok.focus()
    await key('Tab')
    expect(document.activeElement).toBe(close)
    await key('Tab', true)
    expect(document.activeElement).toBe(ok)
  })

  it('closes on Escape and returns focus to the opener', async () => {
    await mountPage()
    await key('Escape')
    expect(dialog().exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('#opener').element)
    expect(document.body.style.overflow).toBe('')
  })

  it('closes with the close button and with a click on the backdrop', async () => {
    await mountPage()
    expect(wrapper.get('.ui-dialog__close').attributes('aria-label')).toBe('Закрыть')
    await wrapper.get('.ui-dialog__close').trigger('click')
    expect(dialog().exists()).toBe(false)
    await wrapper.get('#opener').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-backdrop"]').trigger('click')
    expect(dialog().exists()).toBe(false)
  })

  it('is a centred window on the desktop', async () => {
    phone(false)
    await mountPage()
    expect(wrapper.get('.ui-dialog').attributes('data-presentation')).toBe('modal')
  })

  it('turns into a bottom sheet on a phone', async () => {
    const spy = phone(true)
    await mountPage()
    expect(spy).toHaveBeenCalledWith('(max-width: 639px)')
    expect(wrapper.get('.ui-dialog').attributes('data-presentation')).toBe('sheet')
    expect(wrapper.find('.ui-dialog__handle').exists()).toBe(true)
  })
})

describe('UiSheet', () => {
  it('is a bottom sheet at every width with the same keyboard behaviour', async () => {
    phone(false)
    await mountPage(UiSheet)
    expect(wrapper.get('.ui-dialog').attributes('data-presentation')).toBe('sheet')
    expect(dialog().attributes('aria-modal')).toBe('true')
    await key('Escape')
    expect(dialog().exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('#opener').element)
  })
})
