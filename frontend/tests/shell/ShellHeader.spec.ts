// E02-05 (#15): the header of docs/spec.md section 5 — logo, search, "Написать", bell and avatar menu.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ShellHeader from '../../components/shell/ShellHeader.vue'
import { global } from './fixtures'

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const mountHeader = (user: { name: string; avatar?: string } | null, unread = 0) => {
  wrapper = mount(ShellHeader, {
    props: { user, unread },
    slots: { notifications: '<p data-testid="panel-list">список</p>' },
    global,
    attachTo: document.body,
  })
  return wrapper
}

describe('ShellHeader', () => {
  it('has the home link, search and "Написать" for everyone', () => {
    const w = mountHeader(null)
    expect(w.get('a[aria-label="На главную"]').attributes('href')).toBe('/')
    expect(w.get('button[aria-label="Поиск"]').attributes('type')).toBe('button')
    const write = w.get('[data-nav="write"]')
    expect(write.text()).toBe('Написать')
    expect(write.attributes('href')).toBe('/write')
  })

  it('offers "Войти" to a guest instead of the bell and the avatar', () => {
    const w = mountHeader(null)
    expect(w.get('[data-nav="login"]').text()).toBe('Войти')
    expect(w.find('[aria-label="Уведомления"]').exists()).toBe(false)
    expect(w.find('[aria-haspopup="menu"]').exists()).toBe(false)
  })

  it('shows the bell and the avatar menu to a signed-in reader', async () => {
    const w = mountHeader({ name: 'Анна' })
    expect(w.find('[data-nav="login"]').exists()).toBe(false)
    expect(w.get('button[aria-label="Уведомления"]').attributes('aria-expanded')).toBe('false')
    const trigger = w.get('button[aria-haspopup="menu"]')
    expect(trigger.attributes('aria-label')).toBe('Меню профиля: Анна')
    await trigger.trigger('click')
    await flushPromises()
    const items = w.findAll('[role="menuitem"]').map((i) => i.text())
    expect(items).toEqual(['Профиль', 'Черновики', 'Закладки', 'Настройки', 'Выйти'])
  })

  it('names the unread counter on the bell and shows «99+» above 99 (E12-02)', () => {
    const w = mountHeader({ name: 'Анна' }, 3)
    expect(w.get('[data-testid="notifications-bell"]').attributes('aria-label')).toBe(
      'Уведомления, 3 непрочитанных',
    )
    expect(w.get('[data-testid="notifications-badge"]').text()).toBe('3')
    w.unmount()
    const many = mountHeader({ name: 'Анна' }, 120)
    expect(many.get('[data-testid="notifications-badge"]').text()).toBe('99+')
    many.unmount()
    const none = mountHeader({ name: 'Анна' }, 0)
    expect(none.find('[data-testid="notifications-badge"]').exists()).toBe(false)
  })

  it('opens the panel with the list and «Все уведомления», closes it by Esc and by a click outside', async () => {
    const w = mountHeader({ name: 'Анна' }, 1)
    const bell = w.get('[data-testid="notifications-bell"]')
    await bell.trigger('click')
    const panel = w.get('[data-testid="notifications-panel"]')
    expect(bell.attributes('aria-expanded')).toBe('true')
    expect(panel.find('[data-testid="panel-list"]').exists()).toBe(true)
    expect(panel.get('a').attributes('href')).toBe('/notifications')

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()
    expect(w.find('[data-testid="notifications-panel"]').exists()).toBe(false)
    expect(document.activeElement).toBe(bell.element)

    await bell.trigger('click')
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }))
    await flushPromises()
    expect(w.find('[data-testid="notifications-panel"]').exists()).toBe(false)
  })

  it('every icon-only control has a name', () => {
    const w = mountHeader({ name: 'Анна' })
    for (const el of w.findAll('a, button'))
      expect(el.attributes('aria-label') ?? el.text().trim()).not.toBe('')
  })
})
