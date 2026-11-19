import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import UiTabs from '../../components/ui/UiTabs.vue'
import type { TabItem } from '../../types/ui'

const tabs: TabItem[] = [
  { id: 'popular', label: 'Популярное' },
  { id: 'fresh', label: 'Свежее' },
  { id: 'archive', label: 'Архив', disabled: true },
  { id: 'mine', label: 'Моя лента' },
]
const slots = { popular: 'Попул', fresh: 'Свеж', archive: 'Арх', mine: 'Моё' }

const mountTabs = (modelValue?: string) =>
  mount(UiTabs, {
    props: {
      tabs,
      label: 'Лента',
      modelValue,
      'onUpdate:modelValue': (v: string | undefined) => wrapper.setProps({ modelValue: v }),
    },
    slots,
    attachTo: document.body,
  })
let wrapper: ReturnType<typeof mountTabs>
const selected = () => wrapper.get('[aria-selected="true"]')
const key = async (key: string) => {
  document.activeElement!.dispatchEvent(
    new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }),
  )
  await flushPromises()
}

afterEach(() => wrapper?.unmount())

describe('UiTabs', () => {
  it('selects the first tab by default and links tabs with their panels', () => {
    wrapper = mountTabs()
    expect(wrapper.get('[role="tablist"]').attributes('aria-label')).toBe('Лента')
    expect(selected().text()).toBe('Популярное')
    for (const tab of wrapper.findAll('[role="tab"]')) {
      const panel = wrapper.get(`[id="${tab.attributes('aria-controls')}"]`)
      expect(panel.attributes('role')).toBe('tabpanel')
      expect(panel.attributes('aria-labelledby')).toBe(tab.attributes('id'))
    }
    const visible = wrapper.findAll('[role="tabpanel"]').filter((p) => p.attributes('hidden') === undefined)
    expect(visible.map((p) => p.text())).toEqual(['Попул'])
  })

  it('keeps only the selected tab in the Tab order', () => {
    wrapper = mountTabs('fresh')
    expect(wrapper.findAll('[role="tab"]').map((t) => t.attributes('tabindex'))).toEqual([
      '-1',
      '0',
      '-1',
      '-1',
    ])
  })

  it('selects on click and emits v-model', async () => {
    wrapper = mountTabs()
    await wrapper.findAll('[role="tab"]')[1]!.trigger('click')
    expect(wrapper.emitted('update:modelValue')!.at(-1)).toEqual(['fresh'])
    expect(selected().text()).toBe('Свежее')
  })

  it('moves and selects with the arrows, wrapping and skipping disabled tabs, and with Home and End', async () => {
    wrapper = mountTabs()
    ;(selected().element as HTMLElement).focus()
    await key('ArrowRight')
    expect(selected().text()).toBe('Свежее')
    await key('ArrowRight')
    expect(selected().text()).toBe('Моя лента')
    expect(document.activeElement).toBe(selected().element)
    await key('ArrowRight')
    expect(selected().text()).toBe('Популярное')
    await key('ArrowLeft')
    expect(selected().text()).toBe('Моя лента')
    await key('Home')
    expect(selected().text()).toBe('Популярное')
    await key('End')
    expect(selected().text()).toBe('Моя лента')
  })

  it('does not select a disabled tab', async () => {
    wrapper = mountTabs()
    await wrapper.findAll('[role="tab"]')[2]!.trigger('click')
    expect(selected().text()).toBe('Популярное')
  })
})
