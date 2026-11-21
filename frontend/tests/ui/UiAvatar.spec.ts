import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import UiAvatar from '../../components/ui/UiAvatar.vue'
import { hydrate } from './fixtures/hydrate'

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

  it('falls back to the initials when the picture failed before the page was hydrated', async () => {
    const proto = HTMLImageElement.prototype
    const complete = vi.spyOn(proto, 'complete', 'get').mockReturnValue(true)
    const width = vi.spyOn(proto, 'naturalWidth', 'get').mockReturnValue(0)
    const avatar = await hydrate(UiAvatar, { name: 'Анна Котова', src: '/missing.png' })
    complete.mockRestore()
    width.mockRestore()
    expect(avatar.textContent).toBe('АК')
  })

  it('offers its sources for the avatar size, so the browser takes the width for the screen density', () => {
    const sources = [64, 128, 192].map((width) => ({ url: `/a-${width}.webp`, width }))
    const img = mount(UiAvatar, { props: { name: 'Анна', sources, size: 'sm' } }).get('img')
    expect(img.attributes('srcset')).toBe('/a-64.webp 64w, /a-128.webp 128w, /a-192.webp 192w')
    expect(img.attributes('sizes')).toBe('32px')
    expect(img.attributes('width')).toBe('32')
    expect(img.attributes('loading')).toBe('lazy')
  })

  it('hides a decorative avatar from screen readers', () => {
    const img = mount(UiAvatar, { props: { name: 'Анна', src: '/a.png', decorative: true } }).get('img')
    expect(img.attributes('alt')).toBe('')
    const initials = mount(UiAvatar, { props: { name: 'Анна', decorative: true } })
    expect(initials.attributes('aria-hidden')).toBe('true')
    expect(initials.attributes('role')).toBeUndefined()
  })
})
