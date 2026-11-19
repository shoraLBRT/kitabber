import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { colors, scales, themes } from '../../brand/tokens'
import TokenShowcase from '../components/TokenShowcase.vue'

describe('TokenShowcase', () => {
  const wrapper = mount(TokenShowcase)

  it.each(themes)('shows every colour token of the %s palette inside that theme', (theme) => {
    const section = wrapper.get(`[data-testid="colors-${theme}"]`)
    expect(section.attributes('data-theme')).toBe(theme)
    for (const [name, hex] of Object.entries(colors(theme))) {
      expect(section.text()).toContain(name)
      expect(section.text()).toContain(hex)
    }
    expect(section.find('.tokens__reading').exists()).toBe(true)
  })

  it('shows every typography, spacing, radius and size token', () => {
    const text = wrapper.text()
    for (const name of Object.keys(scales())) expect(text).toContain(name)
    expect(wrapper.findAll('[data-testid="type-scale"] p').length).toBe(
      Object.keys(scales()).filter((n) => n.startsWith('--font-size-')).length,
    )
  })
})
