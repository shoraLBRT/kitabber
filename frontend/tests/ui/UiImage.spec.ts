import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'

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

// E06-03 (ADR-0067): the blurred stand-in and the dominant colour until the picture arrives.
describe('UiImage preview', () => {
  const preview = { color: '#2080c0', image: 'data:image/webp;base64,UklGRgAAAABXRUJQ' }

  it('fills the box with the colour and the blurred tiny picture in the server HTML', async () => {
    // The server's HTML, as the browser gets it before any script: jsdom's style parser would drop the SVG link.
    const html = await renderToString(
      createSSRApp({
        render: () => h(UiImage, { src: '/p.webp', width: 320, height: 200, alt: '', preview }),
      }),
    )
    const container = document.createElement('div')
    container.innerHTML = html
    const img = container.querySelector('img')!
    expect(img.hasAttribute('data-preview')).toBe(true)
    const style = decodeURIComponent(img.getAttribute('style') ?? '')
    expect(style).toContain('background-color:#2080c0')
    expect(style).toContain('url("data:image/svg+xml,')
    expect(style).toContain('feGaussianBlur')
    // Blurred by about one of the tiny picture's 16 pixels, over the box's own proportions.
    expect(style).toContain("stdDeviation='20'")
    expect(style).toContain("viewBox='0 0 320 200'")
    expect(style).toContain(preview.image)
  })

  it('drops the stand-in once the picture has painted, and after an error', async () => {
    const loaded = mount(UiImage, { props: { src: '/p.webp', width: 4, height: 3, alt: '', preview } })
    await loaded.get('img').trigger('load')
    expect(loaded.get('img').attributes('data-preview')).toBeUndefined()
    expect(loaded.get('img').attributes('style')).not.toContain('background')

    const broken = mount(UiImage, { props: { src: '/p.webp', width: 4, height: 3, alt: '', preview } })
    await broken.get('img').trigger('error')
    expect(broken.get('[data-state="error"]').attributes('style')).not.toContain('background')
  })

  it('keeps the neutral surface without a preview, and takes only a colour and a base64 picture', () => {
    const plain = mount(UiImage, { props: { src: '/p.webp', width: 4, height: 3, alt: '' } }).get('img')
    expect(plain.attributes('data-preview')).toBeUndefined()
    expect(plain.attributes('style')).not.toContain('background')

    const hostile = mount(UiImage, {
      props: {
        src: '/p.webp',
        width: 4,
        height: 3,
        alt: '',
        preview: { color: 'red; position: fixed', image: "https://evil.example/x.png') ; x: url('" },
      },
    }).get('img')
    expect(hostile.attributes('data-preview')).toBeUndefined()
    expect(hostile.attributes('style')).not.toContain('evil')
    expect(hostile.attributes('style')).not.toContain('fixed')
  })
})
