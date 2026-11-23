import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import UiSkeleton from '../../components/ui/UiSkeleton.vue'

// The skeleton (E02-07, ADR-0057): its size comes from the tokens of the content it stands for. The sizes on a real
// page are measured in e2e/states.spec.ts.
describe('UiSkeleton', () => {
  it('is a line of the text of the given font size by default, hidden from screen readers', () => {
    const line = mount(UiSkeleton)
    expect(line.classes()).toEqual(['ui-skeleton', 'ui-skeleton--text', 'ui-skeleton--text-md'])
    expect(line.attributes('aria-hidden')).toBe('true')

    const title = mount(UiSkeleton, { props: { size: 'xl', width: '70%' } })
    expect(title.classes()).toContain('ui-skeleton--text-xl')
    expect(title.attributes('style')).toContain('width: 70%')
  })

  it('is the circle of an avatar of the given size, whatever width it is given', () => {
    const avatar = mount(UiSkeleton, { props: { shape: 'avatar', size: 'sm', width: '50%' } })
    expect(avatar.classes()).toContain('ui-skeleton--avatar-sm')
    expect(avatar.attributes('style') ?? '').not.toContain('width')
  })

  it('is a block of a height or of the proportions of a picture', () => {
    const fixed = mount(UiSkeleton, { props: { shape: 'block', height: '12rem' } })
    expect(fixed.attributes('style')).toContain('height: 12rem')

    const picture = mount(UiSkeleton, { props: { shape: 'block', ratio: 1.5 } })
    expect(picture.attributes('style')).toContain('aspect-ratio: 1.5')
  })

  it('takes no height from props for a line: the line box of its text sets it', () => {
    const line = mount(UiSkeleton, { props: { height: '5rem' } })
    expect(line.attributes('style') ?? '').not.toContain('height')
  })
})
