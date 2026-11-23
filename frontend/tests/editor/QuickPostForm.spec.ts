import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import QuickPostForm from '../../components/editor/QuickPostForm.vue'
import type { PublishInput, PublishOutcome } from '../../api/publish'
import { imageMessages, type ImageUpload } from '../../editor/image'
import type { QuickPostHandoff } from '../../editor/quick-post'

// The quick post above the feed (E05-29, docs/ui/editor.md, ADR-0055): plain text and photos published without the
// full editor, the checks of publishing (E05-09: an empty post is not sent, the server's refusal is shown and the text
// stays), «Развернуть» with everything written, and an invitation to sign in for a guest.
const topics = [{ id: '0195f3a0-0000-7000-8000-0000000000t1', name: 'Книги', nickname: 'books' }]
const published: PublishOutcome = {
  status: 'published',
  post: {} as never,
  path: '/u/anna/0195f3a0-0000-7000-8000-000000000001',
}

let wrapper: VueWrapper

function mountForm(
  options: {
    signedIn?: boolean
    publish?: (input: PublishInput) => Promise<PublishOutcome>
    uploadImage?: (file: File) => Promise<ImageUpload>
    measure?: (url: string) => Promise<{ width: number; height: number } | null>
  } = {},
) {
  wrapper = mount(QuickPostForm, {
    attachTo: document.body,
    props: {
      topics,
      signedIn: options.signedIn ?? true,
      publish: options.publish ?? vi.fn(async () => published),
      uploadImage:
        options.uploadImage ?? vi.fn(async (): Promise<ImageUpload> => ({ status: 'uploaded', media: 'm1' })),
      measure: options.measure ?? (async () => ({ width: 40, height: 30 })),
      preview: (file: File) => `blob:${file.name}`,
    },
  })
  return wrapper
}

async function attach(...names: string[]) {
  const input = wrapper.get<HTMLInputElement>('[data-testid="quick-post-file"]')
  const files = names.map((name) => new File(['x'], name, { type: 'image/jpeg' }))
  Object.defineProperty(input.element, 'files', { value: files, configurable: true })
  await input.trigger('change')
  await flushPromises()
}

const publishButton = () => wrapper.get<HTMLButtonElement>('[data-testid="quick-post-publish"]')

afterEach(() => wrapper?.unmount())

describe('QuickPostForm', () => {
  it('shows a guest an invitation to sign in instead of the form', async () => {
    mountForm({ signedIn: false })
    expect(wrapper.find('[data-testid="quick-post-text"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="quick-post-guest"]').text()).toContain('Войдите, чтобы написать пост.')
    await wrapper.get('[data-testid="quick-post-sign-in"]').trigger('click')
    expect(wrapper.emitted('sign-in')).toHaveLength(1)
  })

  it('does not publish an empty post', async () => {
    const publish = vi.fn(async () => published)
    mountForm({ publish })
    expect(publishButton().element.disabled).toBe(true)
    await wrapper.get('[data-testid="quick-post-text"]').setValue('   \n  ')
    expect(publishButton().element.disabled).toBe(true)
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publish).not.toHaveBeenCalled()
  })

  it('publishes the text without a title into the chosen topic and clears the form', async () => {
    const publish = vi.fn(async (_: PublishInput) => published)
    mountForm({ publish })
    await wrapper.get('[data-testid="quick-post-text"]').setValue('Привет, <мир>\n\nВторой абзац')
    await wrapper.get('[data-testid="quick-post-topic"] button').trigger('click')
    const item = wrapper.findAll('[role="menuitem"]').find((element) => element.text() === 'Книги')!
    await item.trigger('click')
    expect(publishButton().element.disabled).toBe(false)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const input = publish.mock.calls[0]![0]
    expect(input.title).toBeNull()
    expect(input.draftId).toBeNull()
    expect(input.blogId).toBe(topics[0]!.id)
    expect(input.body.blocks.map((block) => block.data)).toEqual([
      { text: 'Привет, &lt;мир&gt;' },
      { text: 'Второй абзац' },
    ])
    expect(wrapper.emitted('published')).toEqual([[published.path]])
    expect(wrapper.get<HTMLTextAreaElement>('[data-testid="quick-post-text"]').element.value).toBe('')
  })

  it('publishes with Ctrl+Enter from the text', async () => {
    const publish = vi.fn(async () => published)
    mountForm({ publish })
    const text = wrapper.get('[data-testid="quick-post-text"]')
    await text.setValue('Коротко')
    await text.trigger('keydown', { key: 'Enter', ctrlKey: true })
    await flushPromises()
    expect(publish).toHaveBeenCalledTimes(1)
  })

  it('uploads attached photos with an indicator and publishes them after the text', async () => {
    let finish!: (outcome: ImageUpload) => void
    const uploadImage = vi.fn(() => new Promise<ImageUpload>((resolve) => (finish = resolve)))
    const publish = vi.fn(async (_: PublishInput) => published)
    mountForm({ uploadImage, publish })
    await wrapper.get('[data-testid="quick-post-text"]').setValue('С фото')
    await attach('photo.jpg')

    const photo = wrapper.get('[data-testid="quick-post-photo"]')
    expect(photo.attributes('data-state')).toBe('uploading')
    expect(photo.text()).toContain(imageMessages.uploading)
    expect(publishButton().element.disabled).toBe(true)
    expect(wrapper.get<HTMLButtonElement>('[data-testid="quick-post-expand"]').element.disabled).toBe(true)

    finish({ status: 'uploaded', media: 'm1' })
    await flushPromises()
    expect(wrapper.get('[data-testid="quick-post-photo"]').attributes('data-state')).toBe('ready')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(publish.mock.calls[0]![0].body.blocks).toEqual([
      expect.objectContaining({ type: 'paragraph', data: { text: 'С фото' } }),
      expect.objectContaining({ type: 'image', data: { media: 'm1', width: 40, height: 30, caption: '' } }),
    ])
  })

  it('publishes a photo without text', async () => {
    const publish = vi.fn(async (_: PublishInput) => published)
    mountForm({ publish })
    await attach('photo.jpg')
    expect(publishButton().element.disabled).toBe(false)
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publish.mock.calls[0]![0].body.blocks.map((block) => block.type)).toEqual(['image'])
  })

  it('shows why a photo failed, retries it, and leaves out a removed one', async () => {
    const uploadImage = vi
      .fn<(file: File) => Promise<ImageUpload>>()
      .mockResolvedValueOnce({ status: 'unsupported' })
      .mockResolvedValueOnce({ status: 'uploaded', media: 'm1' })
    mountForm({ uploadImage })
    await attach('photo.jpg')
    const photo = () => wrapper.get('[data-testid="quick-post-photo"]')
    expect(photo().attributes('data-state')).toBe('error')
    expect(photo().get('[role="alert"]').text()).toContain(imageMessages.unsupported)
    expect(publishButton().element.disabled).toBe(true)

    await photo().get('[data-testid="quick-post-photo-retry"]').trigger('click')
    await flushPromises()
    expect(photo().attributes('data-state')).toBe('ready')

    await photo().get('[data-testid="quick-post-photo-remove"]').trigger('click')
    expect(wrapper.find('[data-testid="quick-post-photo"]').exists()).toBe(false)
    expect(publishButton().element.disabled).toBe(true)
  })

  it('says a picked file that is not a picture cannot be read, without uploading it', async () => {
    const uploadImage = vi.fn(async (): Promise<ImageUpload> => ({ status: 'uploaded', media: 'm1' }))
    mountForm({ uploadImage, measure: async () => null })
    await attach('broken.jpg')
    expect(wrapper.get('[data-testid="quick-post-photo"]').text()).toContain(imageMessages.unreadable)
    expect(wrapper.find('[data-testid="quick-post-photo-retry"]').exists()).toBe(false)
    expect(uploadImage).not.toHaveBeenCalled()
  })

  it('keeps the text and shows the refusal when publishing fails, and tries again with the same draft', async () => {
    const draftId = '0195f3a0-0000-7000-8000-0000000000d1'
    const publish = vi
      .fn<(input: PublishInput) => Promise<PublishOutcome>>()
      .mockResolvedValueOnce({ status: 'error', draftId })
      .mockResolvedValueOnce({ status: 'email-not-confirmed', draftId })
    mountForm({ publish })
    await wrapper.get('[data-testid="quick-post-text"]').setValue('Не уйдёт')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[data-testid="quick-post-error"]').text()).toBe(
      'Не удалось опубликовать пост. Попробуйте ещё раз.',
    )
    expect(wrapper.get<HTMLTextAreaElement>('[data-testid="quick-post-text"]').element.value).toBe('Не уйдёт')

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publish.mock.calls[1]![0].draftId).toBe(draftId)
    expect(wrapper.get('[data-testid="quick-post-error"]').text()).toContain('подтвердите почту')
  })

  it('asks for the sign-in when the session is gone', async () => {
    mountForm({ publish: async () => ({ status: 'unauthorized' }) })
    await wrapper.get('[data-testid="quick-post-text"]').setValue('Текст')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('unauthorized')).toHaveLength(1)
  })

  it('hands the text, the photos and the topic to the full editor with «Развернуть»', async () => {
    mountForm()
    await wrapper.get('[data-testid="quick-post-text"]').setValue('Длинная мысль')
    await wrapper.get('[data-testid="quick-post-topic"] button').trigger('click')
    await wrapper
      .findAll('[role="menuitem"]')
      .find((element) => element.text() === 'Книги')!
      .trigger('click')
    await attach('photo.jpg')
    await wrapper.get('[data-testid="quick-post-expand"]').trigger('click')

    const [handoff] = wrapper.emitted<[QuickPostHandoff]>('expand')![0]!
    expect(handoff.topicId).toBe(topics[0]!.id)
    expect(handoff.document.blocks).toEqual([
      expect.objectContaining({ type: 'paragraph', data: { text: 'Длинная мысль' } }),
      expect.objectContaining({ type: 'image', data: { media: 'm1', width: 40, height: 30, caption: '' } }),
    ])
  })
})
