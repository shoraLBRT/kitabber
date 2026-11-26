// Acceptance of E04-08 (#69): the settings have the sections «Блог», «Ленты», «Основные», «Уведомления» (and the
// existing «Аккаунт и вход» and «Настройки cookie»), each at an address of its own with a page there; the desktop
// shows them in the right column, the phone as the list of /settings with rows of at least 44 px.
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import SettingsNav from '../../components/settings/SettingsNav.vue'
import { accountNav, settingsNav, settingsPath } from '../../utils/navigation'
import { global } from '../shell/fixtures'

const frontend = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('the settings sections', () => {
  it('are the four of the issue first, then the account and the cookies, each at its own address', () => {
    expect(settingsNav.map((item) => [item.label, item.to])).toEqual([
      ['Блог', '/settings/blog'],
      ['Ленты', '/settings/feeds'],
      ['Основные', '/settings/general'],
      ['Уведомления', '/settings/notifications'],
      ['Приватность и блокировки', '/settings/privacy'],
      ['Аккаунт и вход', '/settings/account'],
      ['Настройки cookie', '/settings/cookies'],
    ])
    expect(new Set(settingsNav.map((item) => item.to)).size).toBe(settingsNav.length)
  })

  it('each has a page at its address that puts the sections in the right column', () => {
    for (const item of [...settingsNav]) {
      const file = join(frontend, 'pages', `${item.to.slice(1)}.vue`)
      expect(existsSync(file), file).toBe(true)
      expect(readFileSync(file, 'utf8')).toContain('settingsNav: true')
    }
    const list = readFileSync(join(frontend, 'pages', 'settings', 'index.vue'), 'utf8')
    expect(list).toContain('variant="list"')
    expect(readFileSync(join(frontend, 'layouts', 'default.vue'), 'utf8')).toContain('route.meta.settingsNav')
  })

  it('the account menu leads to the list', () => {
    expect(accountNav.find((item) => item.id === 'settings')?.to).toBe(settingsPath)
  })
})

describe('SettingsNav', () => {
  it('marks the open section in the right column', () => {
    wrapper = mount(SettingsNav, { props: { currentPath: '/settings/general' }, global })
    expect(wrapper.get('nav').attributes('aria-label')).toBe('Разделы настроек')
    expect(wrapper.get('h2').text()).toBe('Настройки')
    const links = wrapper.findAll('a')
    expect(links.map((link) => link.attributes('href'))).toEqual(settingsNav.map((item) => item.to))
    expect(
      links.filter((link) => link.attributes('aria-current') === 'page').map((link) => link.text()),
    ).toEqual(['Основные'])
  })

  it('as the list of a phone, has no heading of its own and a chevron in every row', () => {
    wrapper = mount(SettingsNav, { props: { currentPath: settingsPath, variant: 'list' }, global })
    expect(wrapper.find('h2').exists()).toBe(false)
    expect(wrapper.findAll('a')).toHaveLength(settingsNav.length)
    expect(wrapper.findAll('.settings-nav__chevron')).toHaveLength(settingsNav.length)
    expect(wrapper.findAll('[aria-current]')).toHaveLength(0)
  })
})
