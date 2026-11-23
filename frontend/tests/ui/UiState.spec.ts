import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import UiState from '../../components/ui/UiState.vue'

// A message in place of the content (E02-07, FR-UI, ADR-0057): empty, error, not found.
describe('UiState', () => {
  it('shows an empty state as a quiet text without an alert', () => {
    const wrapper = mount(UiState, { slots: { default: 'Здесь пока нет постов.' } })
    expect(wrapper.attributes('data-tone')).toBe('empty')
    expect(wrapper.text()).toBe('Здесь пока нет постов.')
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.find('h2').exists()).toBe(false)
    expect(wrapper.find('.ui-state__actions').exists()).toBe(false)
  })

  it('tells a failure to screen readers at once, with the icon and the actions', () => {
    const wrapper = mount(UiState, {
      props: { tone: 'error' },
      slots: { default: 'Не удалось загрузить.', actions: '<button>Повторить</button>' },
    })
    const alert = wrapper.get('[role="alert"]')
    expect(alert.text()).toBe('Не удалось загрузить.')
    expect(alert.find('svg[aria-hidden="true"]').exists()).toBe(true)
    expect(wrapper.get('.ui-state__actions button').text()).toBe('Повторить')
  })

  it('makes the title the heading of the level asked for', () => {
    const page = mount(UiState, {
      props: { tone: 'not-found', title: 'Страница не найдена', heading: 'h1' },
      slots: { default: 'Адрес набран с ошибкой.' },
    })
    expect(page.get('h1').text()).toBe('Страница не найдена')
    expect(page.find('[role="alert"]').exists()).toBe(false)

    const inside = mount(UiState, { props: { title: 'Пусто' } })
    expect(inside.get('h2').text()).toBe('Пусто')
  })
})
