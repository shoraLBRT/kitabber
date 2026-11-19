// Acceptance of E02-06 (#16): the "Меню" screen opens from the bottom bar and replaces the left column — profile or
// "Войти", drafts, bookmarks, settings, topics, the theme switch and the service links, without "коллекция",
// "подписка" or monetization (docs/spec.md section 5, docs/ui/README.md).
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ShellMenuScreen from '../../components/shell/ShellMenuScreen.vue'
import ShellTabBar from '../../components/shell/ShellTabBar.vue'
import { accountNav, loginPath, menuPath, serviceNav, topicsPath } from '../../utils/navigation'
import { global } from './fixtures'

const frontend = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const mountMenu = (props: Record<string, unknown> = {}) => {
  wrapper = mount(ShellMenuScreen, {
    props: { currentPath: menuPath, ...props },
    global,
    attachTo: document.body,
  })
  return wrapper
}
const hrefs = (w: ReturnType<typeof mount>) => w.findAll('a').map((a) => a.attributes('href'))

describe('ShellMenuScreen', () => {
  it('opens from the "Меню" tab of the bottom bar: the tab links to the page that shows this screen', () => {
    const bar = mount(ShellTabBar, { props: { currentPath: '/' }, global })
    expect(bar.get('[data-nav="menu"]').attributes('href')).toBe(menuPath)
    bar.unmount()
    const page = readFileSync(join(frontend, 'pages', `${menuPath.slice(1)}.vue`), 'utf8')
    expect(page).toContain('<ShellMenuScreen')
  })

  it('is titled "Меню"', () => {
    const w = mountMenu()
    const title = w.get('h1')
    expect(title.text()).toBe('Меню')
    expect(w.get('section').attributes('aria-labelledby')).toBe(title.attributes('id'))
  })

  it('offers a guest "Войти" and no account items', () => {
    const w = mountMenu()
    expect(w.get('[data-nav="login"]').attributes('href')).toBe(loginPath)
    for (const item of accountNav) expect(w.find(`[data-nav="${item.id}"]`).exists()).toBe(false)
    expect(w.text()).not.toContain('Выйти')
  })

  it('shows a reader the profile, drafts, bookmarks and settings, and "Выйти"', async () => {
    const w = mountMenu({ user: { name: 'Анна' } })
    expect(w.find('[data-nav="login"]').exists()).toBe(false)
    expect(w.get('[data-nav="profile"]').text()).toContain('Анна')
    for (const item of accountNav) expect(hrefs(w)).toContain(item.to)
    await w.get('button.menu-screen__logout').trigger('click')
    expect(w.emitted('logout')).toHaveLength(1)
  })

  it('replaces the left column: topics with "Показать все", the theme switch and the service links', () => {
    const w = mountMenu({ topics: [{ id: 'books', label: 'Книги', to: '/books' }] })
    expect(w.text()).toContain('Темы')
    expect(hrefs(w)).toEqual(
      expect.arrayContaining(['/books', topicsPath, ...serviceNav.map((item) => item.to)]),
    )
    expect(w.find('[data-testid="theme-switch"]').exists()).toBe(true)
  })

  it('shows no collections, subscriptions or monetization', () => {
    expect(mountMenu({ user: { name: 'Анна' } }).text()).not.toMatch(/Коллекц|Донат|Подписк|Реклам|Оплат/)
  })

  it('marks the current screen', () => {
    const current = mountMenu({ user: { name: 'Анна' }, currentPath: '/drafts' }).findAll(
      '[aria-current="page"]',
    )
    expect(current.map((a) => a.attributes('data-nav'))).toEqual(['drafts'])
  })
})
