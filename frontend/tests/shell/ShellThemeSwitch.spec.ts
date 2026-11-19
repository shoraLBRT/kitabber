// E02-05 (#15): the colour theme switch of the left menu (FR-UI) — light, dark or system, remembered.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import ShellThemeSwitch from '../../components/shell/ShellThemeSwitch.vue'
import { resetThemeForTests, themeStorageKey } from '../../composables/useTheme'

let wrapper: ReturnType<typeof mount> | undefined
beforeEach(() => {
  resetThemeForTests()
  window.localStorage.clear()
  delete document.documentElement.dataset.theme
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const radios = () => wrapper!.findAll<HTMLInputElement>('input[type="radio"]')

describe('ShellThemeSwitch', () => {
  it('is a named group of three radios, "system" by default', async () => {
    wrapper = mount(ShellThemeSwitch)
    await flushPromises()
    expect(wrapper.get('legend').text()).toBe('Тема оформления')
    expect(radios().map((r) => r.element.value)).toEqual(['system', 'light', 'dark'])
    expect(new Set(radios().map((r) => r.attributes('name'))).size).toBe(1)
    expect(radios()[0]!.element.checked).toBe(true)
    for (const label of wrapper.findAll('label')) expect(label.text()).not.toBe('')
  })

  it('applies and remembers the chosen theme; "system" gives the choice back to the system', async () => {
    wrapper = mount(ShellThemeSwitch)
    await flushPromises()
    await radios()[2]!.setValue(true)
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(window.localStorage.getItem(themeStorageKey)).toBe('dark')
    await radios()[0]!.setValue(true)
    expect(document.documentElement.dataset.theme).toBeUndefined()
    expect(window.localStorage.getItem(themeStorageKey)).toBeNull()
  })

  it('restores the saved theme on load and ignores garbage', async () => {
    window.localStorage.setItem(themeStorageKey, 'light')
    wrapper = mount(ShellThemeSwitch)
    await flushPromises()
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(radios()[1]!.element.checked).toBe(true)
    wrapper.unmount()
    resetThemeForTests()
    delete document.documentElement.dataset.theme
    window.localStorage.setItem(themeStorageKey, 'purple')
    wrapper = mount(ShellThemeSwitch)
    await flushPromises()
    expect(document.documentElement.dataset.theme).toBeUndefined()
    expect(radios()[0]!.element.checked).toBe(true)
  })
})
