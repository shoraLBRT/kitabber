import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import brand from '../../brand/brand.json'
import BrandLogo from '../components/BrandLogo.vue'

describe('BrandLogo', () => {
  it('shows the name and mark from brand.json', () => {
    const html = mount(BrandLogo).html()
    expect(html).toContain(brand.name)
    for (const d of brand.mark.strokes) expect(html).toContain(d)
    expect(html).toContain(brand.palette.light.accent)
  })

  it('uses the dark palette and labels the bare mark', () => {
    const wrapper = mount(BrandLogo, { props: { theme: 'dark', withName: false } })
    expect(wrapper.find('svg').attributes('aria-label')).toBe(brand.name)
    expect(wrapper.html()).toContain(brand.palette.dark.accent)
    expect(wrapper.find('.brand-logo__name').exists()).toBe(false)
  })
})
