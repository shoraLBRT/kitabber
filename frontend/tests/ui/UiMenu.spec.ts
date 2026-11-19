import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import UiMenu from '../../components/ui/UiMenu.vue'
import type { MenuItem } from '../../types/ui'

const items: MenuItem[] = [
  { id: 'profile', label: 'Профиль' },
  { id: 'drafts', label: 'Черновики' },
  { id: 'settings', label: 'Настройки', disabled: true },
  { id: 'logout', label: 'Выйти', danger: true },
]

const mountMenu = () => mount(UiMenu, { props: { items, label: 'Меню профиля' }, attachTo: document.body })
let wrapper: ReturnType<typeof mountMenu>
const active = () => (document.activeElement as HTMLElement).dataset.id
const key = async (key: string) => {
  document.activeElement!.dispatchEvent(
    new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }),
  )
  await flushPromises()
}
const openByClick = async () => {
  await wrapper.get('button').trigger('click')
  await flushPromises()
}

afterEach(() => wrapper?.unmount())

describe('UiMenu', () => {
  it('is a closed menu button that opens on click with focus on the first item', async () => {
    wrapper = mountMenu()
    const trigger = wrapper.get('button')
    expect(trigger.attributes('aria-haspopup')).toBe('menu')
    expect(trigger.attributes('aria-expanded')).toBe('false')
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
    await openByClick()
    const menu = wrapper.get('[role="menu"]')
    expect(trigger.attributes('aria-expanded')).toBe('true')
    expect(trigger.attributes('aria-controls')).toBe(menu.attributes('id'))
    expect(menu.findAll('[role="menuitem"]').map((i) => i.text())).toEqual(items.map((i) => i.label))
    expect(active()).toBe('profile')
  })

  it('opens on the last item with ArrowUp and on the first with ArrowDown', async () => {
    wrapper = mountMenu()
    wrapper.get('button').element.focus()
    await key('ArrowUp')
    expect(active()).toBe('logout')
    await key('Escape')
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
    await key('ArrowDown')
    expect(active()).toBe('profile')
  })

  it('moves with the arrows, wraps around, skips disabled items and jumps with Home and End', async () => {
    wrapper = mountMenu()
    wrapper.get('button').element.focus()
    await key('ArrowDown')
    await key('ArrowDown')
    expect(active()).toBe('drafts')
    await key('ArrowDown')
    expect(active()).toBe('logout')
    await key('ArrowDown')
    expect(active()).toBe('profile')
    await key('ArrowUp')
    expect(active()).toBe('logout')
    await key('Home')
    expect(active()).toBe('profile')
    await key('End')
    expect(active()).toBe('logout')
  })

  it('emits the chosen item, closes and returns focus to the trigger', async () => {
    wrapper = mountMenu()
    await openByClick()
    await wrapper.get('[data-id="drafts"]').trigger('click')
    expect(wrapper.emitted('select')).toEqual([['drafts']])
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('button').element)
  })

  it('ignores a disabled item', async () => {
    wrapper = mountMenu()
    await openByClick()
    const settings = wrapper.get('[data-id="settings"]')
    expect(settings.attributes('aria-disabled')).toBe('true')
    await settings.trigger('click')
    expect(wrapper.emitted('select')).toBeUndefined()
    expect(wrapper.find('[role="menu"]').exists()).toBe(true)
  })

  it('closes on Escape with focus back on the trigger, and on Tab', async () => {
    wrapper = mountMenu()
    await openByClick()
    await key('Escape')
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('button').element)
    await openByClick()
    await key('Tab')
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
  })

  it('closes on a click outside', async () => {
    wrapper = mountMenu()
    await openByClick()
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }))
    await flushPromises()
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
  })

  it('names an icon or avatar trigger by the label', () => {
    wrapper = mount(UiMenu, { props: { items, label: 'Меню профиля' }, slots: { trigger: '<img alt="" />' } })
    expect(wrapper.get('button').attributes('aria-label')).toBe('Меню профиля')
  })
})
