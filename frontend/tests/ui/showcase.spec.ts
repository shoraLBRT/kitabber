// Acceptance of E02-04 (#14): every base component has a section in the showcase (/ui), and the showcase switches
// the theme of the whole page so dialogs and toasts are seen in both palettes.
import { readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import UiShowcase from '../../components/UiShowcase.vue'

const uiDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'components', 'ui')
const components = readdirSync(uiDir)
  .filter((file) => file.endsWith('.vue'))
  .map((file) => file.replace(/\.vue$/, ''))
const tests = readdirSync(dirname(fileURLToPath(import.meta.url)))

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('UI showcase', () => {
  it('covers the base components of the issue', () => {
    for (const name of ['Button', 'Field', 'Menu', 'Tabs', 'Modal', 'Sheet', 'Toast', 'Avatar'])
      expect(components).toContain(`Ui${name}`)
  })

  it.each(components)('has a showcase section for %s', (name) => {
    wrapper = mount(UiShowcase, { global: { stubs: { teleport: true } } })
    expect(wrapper!.find(`[data-testid="showcase-${name}"]`).exists()).toBe(true)
  })

  it.each(components.filter((name) => name !== 'UiToaster' && name !== 'UiSheet'))(
    'has a component test for %s',
    (name) => {
      expect(tests).toContain(`${name}.spec.ts`)
    },
  )

  it('switches the theme of the page and restores the system theme on leave', async () => {
    const page = mount(UiShowcase, { attachTo: document.body, global: { stubs: { teleport: true } } })
    wrapper = page
    const tabs = page.findAll('[role="tablist"]')[0]!.findAll('[role="tab"]')
    await tabs[2]!.trigger('click')
    await flushPromises()
    expect(document.documentElement.dataset.theme).toBe('dark')
    await tabs[1]!.trigger('click')
    await flushPromises()
    expect(document.documentElement.dataset.theme).toBe('light')
    await tabs[0]!.trigger('click')
    await flushPromises()
    expect(document.documentElement.dataset.theme).toBeUndefined()
    await tabs[2]!.trigger('click')
    await flushPromises()
    page.unmount()
    wrapper = undefined
    expect(document.documentElement.dataset.theme).toBeUndefined()
  })
})
