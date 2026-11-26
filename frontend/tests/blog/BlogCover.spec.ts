import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import BlogCover, { type CoverActions } from '../../components/blog/BlogCover.vue'
import { useToast } from '../../composables/useToast'
import type { MediaUpload } from '../../api/media'
import type { CoverInput, Profile, ProfileCover, ProfileSave } from '../../api/profiles'

// The cover of a blog's header (E04-04, docs/ui/blog.md, ADR-0079): shown to everyone at its saved position; the owner
// uploads, moves (mouse, finger, keys), removes and replaces it. The real drag in a browser, on a desktop and on a
// phone, is in e2e/blog-cover.spec.ts.
const cover: ProfileCover = {
  id: 'media-1',
  position: 30,
  sources: [1280, 400, 640].map((width) => ({ url: `https://example.org/img/${width}.webp`, width })),
  preview: { color: '#336699', image: 'data:image/webp;base64,AAAA' },
}

const anna: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: null,
  registeredAt: '2026-03-14T10:00:00Z',
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
  cover: null,
}

const toast = useToast()
beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:picked')
  URL.revokeObjectURL = vi.fn()
})
afterEach(() => toast.clear())

const uploaded: MediaUpload = {
  status: 'uploaded',
  media: {
    id: 'media-2',
    kind: 'image',
    contentType: 'image/jpeg',
    size: 4,
    fileName: 'c.jpg',
    createdAt: '',
    preview: null,
  },
}

function actions(overrides: Partial<CoverActions> = {}) {
  return {
    upload: vi.fn(async (): Promise<MediaUpload> => uploaded),
    save: vi.fn(async (input: CoverInput): Promise<ProfileSave> => ({
      status: 'saved',
      profile: { ...anna, cover: { ...cover, id: input.mediaId, position: input.position ?? 50 } },
    })),
    remove: vi.fn(async (): Promise<ProfileSave> => ({ status: 'saved', profile: anna })),
    ...overrides,
  }
}

// happy-dom lays nothing out: the header is 640 × 192 and the picture a square, so 448 px of it are hidden.
function measure(wrapper: ReturnType<typeof mount>) {
  const box = wrapper.get('.cover__box').element
  Object.defineProperty(box, 'clientWidth', { configurable: true, value: 640 })
  Object.defineProperty(box, 'clientHeight', { configurable: true, value: 192 })
  const img = wrapper.get('img').element
  Object.defineProperty(img, 'naturalWidth', { configurable: true, value: 1000 })
  Object.defineProperty(img, 'naturalHeight', { configurable: true, value: 1000 })
}

function pick(wrapper: ReturnType<typeof mount>, file: File) {
  const input = wrapper.get('[data-testid="blog-cover-file"]')
  Object.defineProperty(input.element, 'files', { configurable: true, value: [file] })
  return input.trigger('change')
}

describe('BlogCover', () => {
  it('shows a visitor the cover at its saved position, with every source and no controls', () => {
    const wrapper = mount(BlogCover, { props: { cover } })
    const img = wrapper.get('[data-testid="blog-cover-image"]')
    expect(img.attributes('style')).toContain('object-position: 50% 30%')
    expect(img.attributes('srcset')).toBe(
      'https://example.org/img/400.webp 400w, https://example.org/img/640.webp 640w, https://example.org/img/1280.webp 1280w',
    )
    expect(img.attributes('src')).toBe('https://example.org/img/1280.webp')
    expect(wrapper.get('.cover__box').attributes('style')).toContain('background-color: #336699')
    expect(wrapper.find('[role="group"]').exists()).toBe(false)
  })

  it('without a cover shows the plain background, and the owner a button to upload one', () => {
    const visitor = mount(BlogCover, { props: { cover: null } })
    expect(visitor.find('img').exists()).toBe(false)
    expect(visitor.find('button').exists()).toBe(false)
    const owner = mount(BlogCover, { props: { cover: null, actions: actions() } })
    expect(owner.get('[data-testid="blog-cover-upload"]').text()).toBe('Загрузить обложку')
  })

  it('moves the cover by dragging with a mouse and saves the position', async () => {
    const owner = actions()
    const wrapper = mount(BlogCover, { props: { cover, actions: owner } })
    await wrapper.get('[data-testid="blog-cover-move"]').trigger('click')
    measure(wrapper)
    const handle = wrapper.get('[data-testid="blog-cover-handle"]')

    // Up by 112 px of 448 hidden: a quarter further down the picture.
    await handle.trigger('pointerdown', { pointerId: 1, clientY: 200, pointerType: 'mouse' })
    await handle.trigger('pointermove', { pointerId: 1, clientY: 88, pointerType: 'mouse' })
    await handle.trigger('pointerup', { pointerId: 1, clientY: 88, pointerType: 'mouse' })
    expect(handle.attributes('aria-valuenow')).toBe('55')
    await wrapper.get('[data-testid="blog-cover-save"]').trigger('click')
    await flushPromises()

    expect(owner.upload).not.toHaveBeenCalled()
    expect(owner.save).toHaveBeenCalledWith({ mediaId: 'media-1', position: 55 })
    expect(wrapper.emitted('saved')?.[0]?.[0]).toMatchObject({ cover: { position: 55 } })
    expect(toast.toasts.at(-1)?.message).toBe('Обложка сохранена')
    expect(wrapper.find('[data-testid="blog-cover-handle"]').exists()).toBe(false)
  })

  it('moves with a finger the same way, and ignores another finger', async () => {
    const wrapper = mount(BlogCover, { props: { cover, actions: actions() } })
    await wrapper.get('[data-testid="blog-cover-move"]').trigger('click')
    measure(wrapper)
    const handle = wrapper.get('[data-testid="blog-cover-handle"]')

    await handle.trigger('pointerdown', { pointerId: 7, clientY: 100, pointerType: 'touch' })
    await handle.trigger('pointermove', { pointerId: 8, clientY: 0, pointerType: 'touch' })
    expect(handle.attributes('aria-valuenow')).toBe('30')
    await handle.trigger('pointermove', { pointerId: 7, clientY: 212, pointerType: 'touch' })
    expect(handle.attributes('aria-valuenow')).toBe('5')
    await handle.trigger('pointercancel', { pointerId: 7, pointerType: 'touch' })
    await handle.trigger('pointermove', { pointerId: 7, clientY: 0, pointerType: 'touch' })
    expect(handle.attributes('aria-valuenow')).toBe('5')
  })

  it('moves with the arrow keys, and «Отмена» keeps the saved position', async () => {
    const owner = actions()
    const wrapper = mount(BlogCover, { props: { cover, actions: owner } })
    await wrapper.get('[data-testid="blog-cover-move"]').trigger('click')
    const handle = wrapper.get('[data-testid="blog-cover-handle"]')
    expect(handle.attributes('role')).toBe('slider')

    await handle.trigger('keydown', { key: 'ArrowUp', shiftKey: true })
    await handle.trigger('keydown', { key: 'ArrowDown' })
    expect(handle.attributes('aria-valuenow')).toBe('39')
    await wrapper.get('[data-testid="blog-cover-cancel"]').trigger('click')

    expect(wrapper.get('img').attributes('style')).toContain('object-position: 50% 30%')
    expect(owner.save).not.toHaveBeenCalled()
  })

  it('uploads a new picture only on «Сохранить», starting in the middle', async () => {
    const owner = actions()
    const wrapper = mount(BlogCover, { props: { cover: null, actions: owner } })
    await pick(wrapper, new File(['jpeg'], 'c.jpg', { type: 'image/jpeg' }))

    expect(wrapper.get('img').attributes('src')).toBe('blob:picked')
    expect(wrapper.get('[data-testid="blog-cover-handle"]').attributes('aria-valuenow')).toBe('50')
    expect(owner.upload).not.toHaveBeenCalled()
    await wrapper.get('[data-testid="blog-cover-save"]').trigger('click')
    await flushPromises()

    expect(owner.upload).toHaveBeenCalledOnce()
    expect(owner.save).toHaveBeenCalledWith({ mediaId: 'media-2', position: 50 })
  })

  it('refuses a file that is not a picture before uploading it', async () => {
    const owner = actions()
    const wrapper = mount(BlogCover, { props: { cover: null, actions: owner } })
    await pick(wrapper, new File(['%PDF'], 'cv.pdf', { type: 'application/pdf' }))

    expect(wrapper.get('[role="alert"]').text()).toBe('Выберите картинку: JPEG, PNG, WebP или GIF.')
    expect(wrapper.find('[data-testid="blog-cover-handle"]').exists()).toBe(false)
  })

  it('shows the loading state while saving and an error that keeps the edit', async () => {
    let answer: (result: ProfileSave) => void = () => {}
    const owner = actions({ save: vi.fn(() => new Promise<ProfileSave>((resolve) => (answer = resolve))) })
    const wrapper = mount(BlogCover, { props: { cover, actions: owner } })
    await wrapper.get('[data-testid="blog-cover-move"]').trigger('click')
    await wrapper.get('[data-testid="blog-cover-handle"]').trigger('keydown', { key: 'End' })
    await wrapper.get('[data-testid="blog-cover-save"]').trigger('click')

    expect(wrapper.get('[data-testid="blog-cover-save"]').attributes('aria-busy')).toBe('true')
    answer({ status: 'error' })
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toBe('Не удалось сохранить обложку. Попробуйте ещё раз.')
    expect(wrapper.get('[data-testid="blog-cover-handle"]').attributes('aria-valuenow')).toBe('100')
    expect(wrapper.emitted('saved')).toBeUndefined()
  })

  it('removes the cover', async () => {
    const owner = actions()
    const wrapper = mount(BlogCover, { props: { cover, actions: owner } })
    await wrapper.get('[data-testid="blog-cover-remove"]').trigger('click')
    await flushPromises()

    expect(owner.remove).toHaveBeenCalledOnce()
    expect(wrapper.emitted('saved')?.[0]?.[0]).toMatchObject({ cover: null })
    expect(toast.toasts.at(-1)?.message).toBe('Обложка удалена')
  })

  it('sends a signed-out owner to sign in', async () => {
    const owner = actions({ remove: vi.fn(async (): Promise<ProfileSave> => ({ status: 'unauthorized' })) })
    const wrapper = mount(BlogCover, { props: { cover, actions: owner } })
    await wrapper.get('[data-testid="blog-cover-remove"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('unauthorized')).toHaveLength(1)
  })
})
