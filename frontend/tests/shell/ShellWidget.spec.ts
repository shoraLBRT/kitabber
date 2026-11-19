// E02-05 (#15): the right column widgets and their shared FR-UI states — empty, loading, error, ready.
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ShellAside from '../../components/shell/ShellAside.vue'
import ShellWidget, { type WidgetState } from '../../components/shell/ShellWidget.vue'

const mountWidget = (state?: WidgetState) =>
  mount(ShellWidget, { props: { title: 'Интересные блоги', state }, slots: { default: '<p>Блог Анны</p>' } })

describe('ShellWidget', () => {
  it('is a section named by its heading', () => {
    const w = mountWidget()
    const id = w.get('h2').attributes('id')
    expect(w.get('section').attributes('aria-labelledby')).toBe(id)
  })

  it('is empty by default, with a quiet note', () => {
    const w = mountWidget()
    expect(w.attributes('data-state')).toBe('empty')
    expect(w.text()).toContain('Пока здесь пусто')
    expect(w.text()).not.toContain('Блог Анны')
  })

  it('shows a skeleton while loading, not a spinner', () => {
    const w = mountWidget('loading')
    expect(w.attributes('aria-busy')).toBe('true')
    expect(w.find('[data-testid="widget-skeleton"]').exists()).toBe(true)
    expect(w.text()).toContain('Загрузка')
  })

  it('shows an error with "Повторить" that asks to load again', async () => {
    const w = mountWidget('error')
    expect(w.get('[role="alert"]').text()).toContain('Не удалось загрузить')
    await w.get('button').trigger('click')
    expect(w.emitted('retry')).toHaveLength(1)
  })

  it('shows its content when ready', () => {
    expect(mountWidget('ready').text()).toContain('Блог Анны')
  })
})

describe('ShellAside', () => {
  it('names the widget that asks to retry', async () => {
    const w = mount(ShellAside, { props: { blogsState: 'error' } })
    await w.get('button').trigger('click')
    expect(w.emitted('retry')).toEqual([['blogs']])
  })
})
