// Acceptance of E04-08 (#69): «Основные» keeps the colour theme in the account — read when the site starts, so the
// choice made on another device holds here, and saved at once; the section shows a skeleton while loading, an error
// with «Повторить», and a failed save puts the previous choice back with a toast. The left menu's switch saves to the
// same place.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Preferences, PreferencesApi } from '../../api/preferences'
import GeneralSettingsForm from '../../components/settings/GeneralSettingsForm.vue'
import ShellThemeSwitch from '../../components/shell/ShellThemeSwitch.vue'
import {
  connectAccountPreferences,
  resetAccountPreferencesForTests,
} from '../../composables/useAccountPreferences'
import { resetThemeForTests, themeStorageKey, type ThemeChoice } from '../../composables/useTheme'
import { useToast } from '../../composables/useToast'

let wrapper: ReturnType<typeof mount> | undefined
beforeEach(() => {
  resetThemeForTests()
  resetAccountPreferencesForTests()
  useToast().clear()
  window.localStorage.clear()
  delete document.documentElement.dataset.theme
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

/** The account on the server: what it holds and what was saved to it. */
function fakeAccount(theme: ThemeChoice, options: { load?: boolean; save?: boolean } = {}) {
  const saved: ThemeChoice[] = []
  const api: PreferencesApi = {
    load: vi.fn(async (): Promise<Preferences | null> =>
      options.load === false
        ? null
        : { general: { theme }, feeds: { freshMinRating: null, homeFeed: 'popular', autoplayVideo: true } },
    ),
    saveFeeds: vi.fn(async () => true),
    saveGeneral: vi.fn(async (value: ThemeChoice) => {
      if (options.save === false) return false
      saved.push(value)
      return true
    }),
  }
  return { api, saved }
}

const radios = () => wrapper!.findAll<HTMLInputElement>('input[type="radio"]')
const checked = () => radios().find((radio) => radio.element.checked)?.element.value
const messages = () => useToast().toasts.map((toast) => toast.message)

describe('GeneralSettingsForm', () => {
  it('shows a skeleton until the account is read', async () => {
    wrapper = mount(GeneralSettingsForm)
    expect(wrapper.find('[data-testid="settings-loading"]').attributes('aria-busy')).toBe('true')
    expect(radios()).toHaveLength(0)
  })

  it('applies the theme chosen on another device over this browser’s', async () => {
    window.localStorage.setItem(themeStorageKey, 'light')
    const { api } = fakeAccount('dark')
    await connectAccountPreferences(api)
    wrapper = mount(GeneralSettingsForm)
    await flushPromises()

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(window.localStorage.getItem(themeStorageKey)).toBe('dark')
    expect(wrapper.get('legend').text()).toBe('Тема оформления')
    expect(radios().map((radio) => radio.element.value)).toEqual(['system', 'light', 'dark'])
    expect(wrapper.findAll('label').map((label) => label.text())).toEqual([
      'Как в системе',
      'Светлая',
      'Тёмная',
    ])
    expect(checked()).toBe('dark')
  })

  it('saves a new choice to the account at once and says so', async () => {
    const { api, saved } = fakeAccount('system')
    await connectAccountPreferences(api)
    wrapper = mount(GeneralSettingsForm)
    await flushPromises()

    await radios()[2]!.setValue(true)
    await flushPromises()

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(saved).toEqual(['dark'])
    expect(messages()).toEqual(['Изменения сохранены'])
  })

  it('puts the previous choice back when the save fails', async () => {
    const { api } = fakeAccount('light', { save: false })
    await connectAccountPreferences(api)
    wrapper = mount(GeneralSettingsForm)
    await flushPromises()

    await radios()[2]!.setValue(true)
    await flushPromises()

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(checked()).toBe('light')
    expect(messages()).toEqual(['Не удалось сохранить. Попробуйте ещё раз.'])
  })

  it('offers «Повторить» when the account could not be read', async () => {
    const { api } = fakeAccount('dark', { load: false })
    await connectAccountPreferences(api)
    wrapper = mount(GeneralSettingsForm)
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toBe('Проверьте соединение и попробуйте ещё раз.')
    vi.mocked(api.load).mockResolvedValueOnce({
      general: { theme: 'dark' },
      feeds: { freshMinRating: null, homeFeed: 'popular', autoplayVideo: true },
    })
    await wrapper.get('button').trigger('click')
    await flushPromises()

    expect(checked()).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
  })
})

describe('the left menu’s theme switch', () => {
  it('saves to the account while signed in', async () => {
    const { api, saved } = fakeAccount('system')
    await connectAccountPreferences(api)
    wrapper = mount(ShellThemeSwitch)
    await flushPromises()

    await radios()[1]!.setValue(true)
    await flushPromises()

    expect(saved).toEqual(['light'])
    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('keeps the choice in this browser only for a guest', async () => {
    wrapper = mount(ShellThemeSwitch)
    await flushPromises()

    await radios()[2]!.setValue(true)
    await flushPromises()

    expect(window.localStorage.getItem(themeStorageKey)).toBe('dark')
    expect(messages()).toEqual([])
  })
})
