// Acceptance of E02-06 (#16): the bottom bar of the phone shell — the five tabs of docs/spec.md section 5, their
// addresses from the one navigation list, the active tab, unread counters and "a second tap scrolls to the top".
// Where the bar is shown (below 925 px) and the safe areas are checked through the style contract in
// phone-layout.spec.ts; the real 390 px page comes with the Playwright profiles (#17).
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ShellTabBar from '../../components/shell/ShellTabBar.vue'
import { menuPath, tabBarNav } from '../../utils/navigation'
import { global } from './fixtures'

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.restoreAllMocks()
})

const mountBar = (props: Record<string, unknown> = {}) => {
  wrapper = mount(ShellTabBar, { props: { currentPath: '/', ...props }, global, attachTo: document.body })
  return wrapper
}

describe('ShellTabBar', () => {
  it('is a navigation landmark with the five tabs of the spec, in order', () => {
    const w = mountBar()
    expect(w.get('nav').attributes('aria-label')).toBe('Нижняя панель')
    expect(w.findAll('a').map((a) => a.find('.tab-bar__label').text())).toEqual([
      'Лента',
      'Рейтинг',
      'Уведомления',
      'Сообщения',
      'Меню',
    ])
    expect(w.text()).not.toMatch(/Коллекц|Донат|Подписк|Реклам/)
  })

  it('links every tab to its own address from the navigation list, the last one to the "Меню" screen', () => {
    const hrefs = mountBar()
      .findAll('a')
      .map((a) => a.attributes('href'))
    expect(hrefs).toEqual(tabBarNav.map((item) => item.to))
    expect(new Set(hrefs).size).toBe(hrefs.length)
    expect(hrefs.at(-1)).toBe(menuPath)
  })

  it('hides the icons from screen readers and keeps the visible label as the name', () => {
    for (const link of mountBar().findAll('a')) {
      expect(link.get('svg').attributes('aria-hidden')).toBe('true')
      expect(link.attributes('aria-label')).toBeUndefined()
    }
  })

  it.each([
    ['/', 'feed'],
    ['/popular', 'feed'],
    ['/new', 'feed'],
    ['/my', 'feed'],
    ['/rating', 'rating'],
    ['/notifications', 'notifications'],
    ['/messages/42', 'messages'],
    ['/menu', 'menu'],
  ])('marks the current tab at %s', (path, id) => {
    const current = mountBar({ currentPath: path }).findAll('[aria-current="page"]')
    expect(current).toHaveLength(1)
    expect(current[0]!.attributes('data-nav')).toBe(id)
  })

  it('marks no tab on a screen outside the bar', () => {
    expect(mountBar({ currentPath: '/u/anna' }).findAll('[aria-current]')).toHaveLength(0)
  })

  it('shows unread counters on the icons and says them to screen readers', () => {
    const w = mountBar({ counts: { notifications: 3, messages: 120, rating: 0 } })
    const badges = w.findAll('[data-testid="tab-badge"]')
    expect(badges.map((b) => b.text())).toEqual(['3', '99+'])
    for (const badge of badges) expect(badge.attributes('aria-hidden')).toBe('true')
    expect(w.get('[data-nav="notifications"]').text()).toContain('непрочитанных: 3')
    expect(w.get('[data-nav="messages"]').text()).toContain('непрочитанных: 120')
    expect(w.get('[data-nav="rating"]').text()).not.toContain('непрочитанных')
  })

  it('shows no counters while there is nothing unread', () => {
    expect(mountBar().findAll('[data-testid="tab-badge"]')).toHaveLength(0)
  })

  it('scrolls to the top on a second tap of the current tab and navigates on any other tab', async () => {
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
    const w = mountBar({ currentPath: '/rating' })
    const current = new MouseEvent('click', { bubbles: true, cancelable: true })
    w.get('[data-nav="rating"]').element.dispatchEvent(current)
    expect(current.defaultPrevented).toBe(true)
    expect(scroll).toHaveBeenCalledWith({ top: 0, behavior: 'smooth' })

    scroll.mockClear()
    const other = new MouseEvent('click', { bubbles: true, cancelable: true })
    w.get('[data-nav="messages"]').element.dispatchEvent(other)
    expect(other.defaultPrevented).toBe(false)
    expect(scroll).not.toHaveBeenCalled()
  })
})
