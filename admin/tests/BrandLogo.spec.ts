import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import brand from '../../brand/brand.json'
import BrandLogo from '../src/components/BrandLogo.vue'

describe('BrandLogo', () => {
  it('shows the name and mark from brand.json', () => {
    const html = mount(BrandLogo).html()
    expect(html).toContain(brand.name)
    for (const d of brand.mark.strokes) expect(html).toContain(d)
    // Colours come from the tokens, so the logo follows the page theme unless one is pinned.
    expect(html).not.toContain(brand.palette.light.accent)
    expect(mount(BrandLogo).attributes('data-theme')).toBeUndefined()
  })

  it('pins the dark palette and labels the bare mark', () => {
    const wrapper = mount(BrandLogo, { props: { theme: 'dark', withName: false } })
    expect(wrapper.find('svg').attributes('aria-label')).toBe(brand.name)
    expect(wrapper.attributes('data-theme')).toBe('dark')
    expect(wrapper.find('.brand-logo__name').exists()).toBe(false)
  })
})
