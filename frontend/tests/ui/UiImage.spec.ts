import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import UiImage from '../../components/ui/UiImage.vue'
import { hydrate } from './fixtures/hydrate'

// The image component (E06-04, ADR-0040): space reserved from the proportions, a srcset for the screen's density,
// lazy loading, and the empty, loading, loaded and error states. What the browser does with them (which source it
// takes, that a picture off the screen is not fetched, that nothing jumps) is in e2e/image.spec.ts.
const sources = [
  { url: '/p-1280.webp', width: 1280 },
  { url: '/p-320.webp', width: 320 },
  { url: '/p-640.webp', width: 640 },
]

describe('UiImage', () => {
  it('offers every source by width, narrowest first, with the sizes and the widest as the fallback', () => {
    const img = mount(UiImage, {
      props: { sources, sizes: '(max-width: 600px) 100vw, 600px', width: 1280, height: 800, alt: 'Горы' },
    }).get('img')
    expect(img.attributes('srcset')).toBe('/p-320.webp 320w, /p-640.webp 640w, /p-1280.webp 1280w')
    expect(img.attributes('sizes')).toBe('(max-width: 600px) 100vw, 600px')
    expect(img.attributes('src')).toBe('/p-1280.webp')
    expect(img.attributes('alt')).toBe('Горы')
  })

  it('takes a given link over the sources, and has no srcset or sizes without sources', () => {
    const both = mount(UiImage, { props: { src: '/p.webp', sources, width: 4, height: 3, alt: '' } }).get(
      'img',
    )
    expect(both.attributes('src')).toBe('/p.webp')
    const single = mount(UiImage, { props: { src: '/p.webp', width: 4, height: 3, alt: '' } }).get('img')
    expect(single.attributes('srcset')).toBeUndefined()
    expect(single.attributes('sizes')).toBeUndefined()
  })

  it('reserves the box from the proportions before the picture arrives', () => {
    const wrapper = mount(UiImage, { props: { src: '/p.webp', width: 320, height: 200, alt: '' } })
    const img = wrapper.get('img')
    expect(img.attributes('width')).toBe('320')
    expect(img.attributes('height')).toBe('200')
    expect(img.attributes('style')).toContain('320 / 200')
  })

  it('loads lazily and decodes off the main thread; eager loads at once and first', () => {
    const lazy = mount(UiImage, { props: { src: '/p.webp', width: 1, height: 1, alt: '' } }).get('img')
    expect(lazy.attributes('loading')).toBe('lazy')
    expect(lazy.attributes('decoding')).toBe('async')
    expect(lazy.attributes('fetchpriority')).toBeUndefined()
    const eager = mount(UiImage, {
      props: { src: '/p.webp', width: 1, height: 1, alt: '', eager: true },
    }).get('img')
    expect(eager.attributes('loading')).toBe('eager')
    expect(eager.attributes('fetchpriority')).toBe('high')
  })

  it('is loading until the picture arrives, then loaded', async () => {
    const wrapper = mount(UiImage, { props: { src: '/p.webp', width: 1, height: 1, alt: '' } })
    expect(wrapper.get('img').attributes('data-state')).toBe('loading')
    await wrapper.get('img').trigger('load')
    expect(wrapper.get('img').attributes('data-state')).toBe('loaded')
    expect(wrapper.emitted('load')).toHaveLength(1)
  })

  it('shows a placeholder of the same box without a link', () => {
    const wrapper = mount(UiImage, { props: { width: 320, height: 200, alt: 'Нет изображения' } })
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.attributes('data-state')).toBe('empty')
    expect(wrapper.attributes('role')).toBe('img')
    expect(wrapper.attributes('aria-label')).toBe('Нет изображения')
    expect(wrapper.attributes('style')).toContain('320 / 200')
  })

  it('shows the error placeholder of the same box when the picture fails, and tries again with a new link', async () => {
    const wrapper = mount(UiImage, { props: { src: '/missing.webp', width: 320, height: 200, alt: 'Горы' } })
    await wrapper.get('img').trigger('error')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.attributes('data-state')).toBe('error')
    expect(wrapper.text()).toContain('Не удалось загрузить')
    expect(wrapper.attributes('aria-label')).toBe('Горы')
    expect(wrapper.attributes('style')).toContain('320 / 200')
    expect(wrapper.emitted('error')).toHaveLength(1)
    await wrapper.setProps({ src: '/other.webp' })
    expect(wrapper.get('img').attributes('data-state')).toBe('loading')
  })

  it('knows a picture that loaded or failed before the page was hydrated', async () => {
    const proto = HTMLImageElement.prototype
    const complete = vi.spyOn(proto, 'complete', 'get').mockReturnValue(true)
    const width = vi.spyOn(proto, 'naturalWidth', 'get').mockReturnValue(0)
    const decode = vi.spyOn(proto, 'decode').mockRejectedValue(new DOMException('broken', 'EncodingError'))
    const failed = await hydrate(UiImage, { src: '/p.webp', width: 1, height: 1, alt: '' })
    // A one-pixel picture taken for a dense screen: a natural width of 0, yet it decodes.
    decode.mockResolvedValue()
    const tiny = await hydrate(UiImage, { src: '/p.webp', width: 1, height: 1, alt: '' })
    width.mockReturnValue(320)
    const loaded = await hydrate(UiImage, { src: '/p.webp', width: 1, height: 1, alt: '' })
    complete.mockRestore()
    width.mockRestore()
    decode.mockRestore()
    expect(failed.getAttribute('data-state')).toBe('error')
    expect(tiny.getAttribute('data-state')).toBe('loaded')
    expect(loaded.getAttribute('data-state')).toBe('loaded')
  })

  it('leaves a lazy picture the browser has not fetched yet loading after hydration', async () => {
    const current = vi.spyOn(HTMLImageElement.prototype, 'currentSrc', 'get').mockReturnValue('')
    const lazy = await hydrate(UiImage, { src: '/p.webp', width: 1, height: 1, alt: '' })
    current.mockRestore()
    expect(lazy.getAttribute('data-state')).toBe('loading')
  })
})
