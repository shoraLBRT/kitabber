import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import UiAvatar from '../../components/ui/UiAvatar.vue'

describe('UiAvatar', () => {
  it('shows the picture with the name as its alt text', () => {
    const img = mount(UiAvatar, { props: { name: 'Анна Котова', src: '/a.png' } }).get('img')
    expect(img.attributes('alt')).toBe('Анна Котова')
  })

  it.each([
    ['Анна Котова', 'АК'],
    ['борис', 'Б'],
    ['  Вера  Лис Петровна ', 'ВЛ'],
    ['', '?'],
  ])('falls back to the initials of "%s"', (name, initials) => {
    const wrapper = mount(UiAvatar, { props: { name } })
    expect(wrapper.text()).toBe(initials)
    expect(wrapper.attributes('role')).toBe('img')
    expect(wrapper.attributes('aria-label')).toBe(name)
  })

  it('falls back to the initials when the picture fails to load', async () => {
    const wrapper = mount(UiAvatar, { props: { name: 'Анна Котова', src: '/missing.png' } })
    await wrapper.get('img').trigger('error')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).toBe('АК')
    await wrapper.setProps({ src: '/other.png' })
    expect(wrapper.find('img').exists()).toBe(true)
  })

  it('falls back to the initials when the picture failed before the page was hydrated', () => {
    const proto = HTMLImageElement.prototype
    const complete = vi.spyOn(proto, 'complete', 'get').mockReturnValue(true)
    const width = vi.spyOn(proto, 'naturalWidth', 'get').mockReturnValue(0)
    const wrapper = mount(UiAvatar, { props: { name: 'Анна Котова', src: '/missing.png' } })
    complete.mockRestore()
    width.mockRestore()
    return nextTick().then(() => expect(wrapper.text()).toBe('АК'))
  })

  it('hides a decorative avatar from screen readers', () => {
    const img = mount(UiAvatar, { props: { name: 'Анна', src: '/a.png', decorative: true } }).get('img')
    expect(img.attributes('alt')).toBe('')
    const initials = mount(UiAvatar, { props: { name: 'Анна', decorative: true } })
    expect(initials.attributes('aria-hidden')).toBe('true')
    expect(initials.attributes('role')).toBeUndefined()
  })
})
