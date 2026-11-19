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

const mountHeader = (user: { name: string; avatar?: string } | null) => {
  wrapper = mount(ShellHeader, { props: { user }, global, attachTo: document.body })
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
    expect(w.get('a[aria-label="Уведомления"]').attributes('href')).toBe('/notifications')
    const trigger = w.get('button[aria-haspopup="menu"]')
    expect(trigger.attributes('aria-label')).toBe('Меню профиля: Анна')
    await trigger.trigger('click')
    await flushPromises()
    const items = w.findAll('[role="menuitem"]').map((i) => i.text())
    expect(items).toEqual(['Профиль', 'Черновики', 'Закладки', 'Настройки', 'Выйти'])
  })

  it('every icon-only control has a name', () => {
    const w = mountHeader({ name: 'Анна' })
    for (const el of w.findAll('a, button'))
      expect(el.attributes('aria-label') ?? el.text().trim()).not.toBe('')
  })
})
