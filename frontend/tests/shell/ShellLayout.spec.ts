// Acceptance of E02-05 (#15): the desktop shell's landmarks, skip link, header, left menu with the active item and
// the right column. Real widths and stickiness are checked through the style contract in layout.spec.ts; the
// browser test at 1440 and 925–1240 px comes with the Playwright profiles (#17).
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ShellLayout from '../../components/shell/ShellLayout.vue'
import { mainNav } from '../../utils/navigation'
import { global } from './fixtures'

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const mountShell = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) => {
  wrapper = mount(ShellLayout, {
    props: { currentPath: '/', ...props },
    slots: { default: '<h1>Экран</h1>', ...slots },
    global,
    attachTo: document.body,
  })
  return wrapper
}

describe('ShellLayout', () => {
  it('has the header, the main menu, the main content and the right column as landmarks', () => {
    const w = mountShell()
    expect(w.findAll('header')).toHaveLength(1)
    expect(w.get('nav').attributes('aria-label')).toBe('Основное меню')
    const main = w.get('main')
    expect(main.attributes('id')).toBe('content')
    expect(main.attributes('tabindex')).toBe('-1')
    expect(main.text()).toContain('Экран')
    expect(w.get('aside').attributes('aria-label')).toBe('Боковая колонка')
  })

  it('places the columns in the order menu, feed, right column', () => {
    const columns = mountShell()
      .findAll('[data-column]')
      .map((c) => c.attributes('data-column'))
    expect(columns).toEqual(['menu', 'feed', 'aside'])
  })

  it('starts with a skip link to the main content', () => {
    const w = mountShell()
    const first = w.element.querySelector('a, button, input')!
    expect(first.textContent).toBe('Перейти к содержимому')
    expect(first.getAttribute('href')).toBe('#content')
    expect(document.getElementById('content')).toBe(w.get('main').element)
  })

  it('keeps the keyboard order of the visual order: header, menu, content, right column', () => {
    const w = mountShell({}, { default: '<button>В ленте</button>' })
    const order = [...w.element.querySelectorAll('a, button, input')].map((el) =>
      el.closest('header') ? 'header' : el.closest('nav') ? 'nav' : el.closest('main') ? 'main' : 'other',
    )
    const collapsed = order.filter((v, i) => v !== order[i - 1])
    expect(collapsed).toEqual(['other', 'header', 'nav', 'main'])
  })

  it('shows the menu items of the spec, without collections or monetization', () => {
    const nav = mountShell().get('nav')
    const text = nav.text()
    for (const label of ['Популярное', 'Свежее', 'Моя лента', 'Сообщения', 'Рейтинг', 'Темы', 'Показать все'])
      expect(text).toContain(label)
    for (const label of ['О проекте', 'Правила', 'Тема оформления']) expect(text).toContain(label)
    expect(text).not.toMatch(/Коллекц|Донат|Подписк|Реклам/)
  })

  it.each([
    ['/', 'popular'],
    ['/popular', 'popular'],
    ['/new', 'new'],
    ['/new/', 'new'],
    ['/my', 'my'],
    ['/messages/42', 'messages'],
    ['/rating', 'rating'],
    ['/rules', 'rules'],
  ])('marks the current screen at %s', (path, id) => {
    const w = mountShell({ currentPath: path })
    const current = w.findAll('nav [aria-current="page"]')
    expect(current).toHaveLength(1)
    expect(current[0]!.attributes('data-nav')).toBe(id)
  })

  it('marks nothing on a screen outside the menu', () => {
    expect(mountShell({ currentPath: '/u/anna' }).findAll('[aria-current]')).toHaveLength(0)
  })

  it('lists the reader topics when there are any', () => {
    const w = mountShell({ topics: [{ id: 'books', label: 'Книги', to: '/books' }], currentPath: '/books' })
    const current = w.get('nav [aria-current="page"]')
    expect(current.text()).toBe('Книги')
  })

  it('links every main menu item to its own address', () => {
    const hrefs = mountShell()
      .findAll('nav [data-nav]')
      .map((a) => a.attributes('href'))
    for (const item of mainNav) expect(hrefs).toContain(item.to)
    expect(new Set(hrefs).size).toBe(hrefs.length)
  })

  it('shows the default right column widgets, and lets a page replace them', () => {
    const w = mountShell()
    const titles = w.findAll('aside h2').map((h) => h.text())
    expect(titles).toEqual(['Популярные комментарии', 'Интересные блоги'])
    wrapper!.unmount()
    const custom = mountShell({}, { aside: '<p>Свой виджет</p>' })
    expect(custom.get('[data-column="aside"]').text()).toBe('Свой виджет')
  })

  it('leaves a slot for the phone shell (#16)', () => {
    const w = mountShell({}, { mobile: '<nav aria-label="Нижняя панель">…</nav>' })
    expect(w.find('nav[aria-label="Нижняя панель"]').exists()).toBe(true)
  })

  it('passes header events up', async () => {
    const w = mountShell({ user: { name: 'Анна' } })
    await w.get('button[aria-label="Поиск"]').trigger('click')
    expect(w.emitted('search')).toHaveLength(1)
    await w.get('button[aria-haspopup="menu"]').trigger('click')
    await flushPromises()
    await w.get('[role="menuitem"][data-id="settings"]').trigger('click')
    expect(w.emitted('account')).toEqual([['settings']])
  })
})
