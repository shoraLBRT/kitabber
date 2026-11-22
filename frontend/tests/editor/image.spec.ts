import type { API, BlockAPI, BlockToolConstructorOptions } from '@editorjs/editorjs'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  captionLength,
  imageCaptionMaxLength,
  imageMessages,
  ImageTool,
  type ImageData,
  type ImageToolConfig,
  type ImageUpload,
} from '../../editor/image'
import { isEmptyDocument, toPostDocument } from '../../editor/document'

// E05-06: the «Фото» block — empty, uploading with an indicator, an error that leaves the document valid, the picture
// with its size, and a caption of limited length.

const file = new File(['x'], 'photo.jpg', { type: 'image/jpeg' })

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => (resolve = done))
  return { promise, resolve }
}

function tool(config: Partial<ImageToolConfig> = {}, data: Partial<ImageData> = {}) {
  const dispatchChange = vi.fn()
  const insert = vi.fn()
  const api = {
    blocks: { getBlockIndex: () => 0, getCurrentBlockIndex: () => 0, insert },
    caret: { setToBlock: vi.fn() },
  } as unknown as API
  const instance = new ImageTool({
    data,
    api,
    block: { id: 'i1', dispatchChange } as unknown as BlockAPI,
    readOnly: false,
    config: {
      upload: async (): Promise<ImageUpload> => ({ status: 'uploaded', media: 'm-1' }),
      measure: async () => ({ width: 800, height: 600 }),
      preview: () => 'blob:preview',
      ...config,
    },
  } as BlockToolConstructorOptions<Partial<ImageData>, ImageToolConfig>)
  const element = instance.render()
  document.body.append(element)
  return { instance, element, dispatchChange, insert }
}

const status = (element: HTMLElement) => element.querySelector('.ce-image__status')!
const caption = (element: HTMLElement) => element.querySelector<HTMLElement>('.ce-image__caption')!

afterEach(() => document.body.replaceChildren())

describe('image block', () => {
  it('starts empty with a button that opens the file picker', () => {
    const { element } = tool()
    const input = element.querySelector<HTMLInputElement>('[data-testid="image-input"]')!
    const click = vi.spyOn(input, 'click').mockImplementation(() => undefined)

    expect(element.dataset.state).toBe('empty')
    expect(input.accept).toBe('image/*')
    element.querySelector<HTMLButtonElement>('[data-testid="image-choose"]')!.click()
    expect(click).toHaveBeenCalledOnce()
    expect(caption(element).hidden).toBe(true)
  })

  it('shows the local preview under an indicator while uploading, then the picture with its size', async () => {
    const upload = deferred<ImageUpload>()
    const { instance, element, dispatchChange } = tool({ upload: () => upload.promise })

    const done = instance.uploadFile(file)
    await vi.waitFor(() => expect(element.dataset.state).toBe('uploading'))
    expect(element.getAttribute('aria-busy')).toBe('true')
    expect(status(element).querySelector('[role="progressbar"]')).not.toBeNull()
    expect(status(element).textContent).toBe(imageMessages.uploading)
    expect(element.querySelector('img')!.getAttribute('src')).toBe('blob:preview')
    expect(instance.save().media).toBe('')

    upload.resolve({ status: 'uploaded', media: 'm-1' })
    await done
    expect(element.dataset.state).toBe('ready')
    expect(element.querySelector('img')!.width).toBe(800)
    expect(element.querySelector('img')!.height).toBe(600)
    expect(instance.save()).toEqual({ media: 'm-1', width: 800, height: 600, caption: '' })
    expect(dispatchChange).toHaveBeenCalledOnce()
  })

  it.each([
    ['unsupported', imageMessages.unsupported],
    ['too-large', imageMessages['too-large']],
    ['unauthorized', imageMessages.unauthorized],
    ['error', imageMessages.error],
  ] as const)('a failed upload (%s) says why and offers a retry', async (outcome, message) => {
    const upload = vi
      .fn<() => Promise<ImageUpload>>()
      .mockResolvedValueOnce({ status: outcome })
      .mockResolvedValueOnce({ status: 'uploaded', media: 'm-2' })
    const { instance, element } = tool({ upload })

    await instance.uploadFile(file)
    expect(element.dataset.state).toBe('error')
    expect(status(element).getAttribute('role')).toBe('alert')
    expect(status(element).textContent).toBe(message)
    expect(instance.save().media).toBe('')

    element.querySelector<HTMLButtonElement>('[data-testid="image-retry"]')!.click()
    await vi.waitFor(() => expect(element.dataset.state).toBe('ready'))
    expect(upload).toHaveBeenCalledTimes(2)
    expect(instance.save().media).toBe('m-2')
  })

  it('a file the browser cannot read is not uploaded', async () => {
    const upload = vi.fn()
    const { instance, element } = tool({ upload, measure: async () => null })

    await instance.uploadFile(file)
    expect(element.dataset.state).toBe('error')
    expect(status(element).textContent).toBe(imageMessages.unreadable)
    expect(upload).not.toHaveBeenCalled()
    expect(element.querySelector('[data-testid="image-retry"]')).toBeNull()
  })

  it('a rejected upload is an error, not an exception', async () => {
    const { instance, element } = tool({ upload: () => Promise.reject(new Error('network')) })

    await instance.uploadFile(file)
    expect(element.dataset.state).toBe('error')
    expect(status(element).textContent).toBe(imageMessages.error)
  })

  it('renders an uploaded image again from its data (undo and redo)', () => {
    const { instance, element } = tool({}, { media: 'm-1', width: 4, height: 3, caption: 'Закат' })

    expect(element.dataset.state).toBe('ready')
    expect(caption(element).innerHTML).toBe('Закат')
    expect(instance.save()).toEqual({ media: 'm-1', width: 4, height: 3, caption: 'Закат' })
  })

  it('refuses typing past the caption limit and cuts anything longer', () => {
    const { element } = tool({}, { media: 'm-1', width: 4, height: 3 })
    const field = caption(element)
    field.textContent = 'а'.repeat(imageCaptionMaxLength)

    const typed = new InputEvent('beforeinput', { inputType: 'insertText', data: 'б', cancelable: true })
    field.dispatchEvent(typed)
    expect(typed.defaultPrevented).toBe(true)

    field.textContent = 'а'.repeat(imageCaptionMaxLength + 20)
    field.dispatchEvent(new Event('input'))
    expect(field.textContent).toHaveLength(imageCaptionMaxLength)
  })

  it('Enter in the caption goes on with a text block after the picture', () => {
    const { element, insert } = tool({}, { media: 'm-1', width: 4, height: 3 })
    const enter = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true, bubbles: true })

    caption(element).dispatchEvent(enter)
    expect(enter.defaultPrevented).toBe(true)
    expect(insert).toHaveBeenCalledWith('paragraph', { text: '' }, undefined, 1, true)
  })

  it('counts the caption as a reader sees it', () => {
    expect(captionLength('<b>ab</b>&amp;<br>c')).toBe(5)
  })
})

describe('image block in the document', () => {
  const output = (data: Record<string, unknown>) => ({
    blocks: [
      { id: 'p1', type: 'paragraph', data: { text: 'Текст' } },
      { id: 'i1', type: 'image', data },
    ],
  })

  it('keeps an uploaded image with its size and a cleaned caption', () => {
    const document = toPostDocument(
      output({ media: 'm-1', width: 800, height: 600, caption: 'Закат <span style="x">над</span> морем' }),
    )

    expect(document.blocks[1]).toEqual({
      id: 'i1',
      type: 'image',
      data: { media: 'm-1', width: 800, height: 600, caption: 'Закат над морем' },
    })
  })

  it('leaves out an image still uploading or failed, so the document stays valid', () => {
    const document = toPostDocument(output({ media: '', width: 0, height: 0, caption: 'Подпись' }))

    expect(document.blocks.map((block) => block.type)).toEqual(['paragraph'])
  })

  it('an image alone is not an empty post', () => {
    expect(
      isEmptyDocument({
        version: 1,
        blocks: [{ id: 'i1', type: 'image', data: { media: 'm-1', width: 1, height: 1, caption: '' } }],
      }),
    ).toBe(false)
  })
})
