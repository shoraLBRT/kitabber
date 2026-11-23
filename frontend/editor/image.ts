// The «Фото» block of the editor (E05-06, ADR-0049): one uploaded image and its caption. The block names the file by
// the id the media service gave (`media`) and keeps the picture's size (`width`, `height`) so the reader page holds
// its place before it loads (ADR-0040); the delivery links come with the post (PostView.images), not in the document.
// States, on the figure as data-state: `empty` (a button that opens the file picker; the picker opens by itself when
// the block is added from the menu), `uploading` (the local preview under an indicator), `error` (the reason, «Повторить»
// and «Выбрать другой файл»; the rest of the document is untouched) and `ready`. A block without an uploaded file is
// left out of the saved document (document.ts), so a failed upload never breaks it. The caption is one line of at most
// imageCaptionMaxLength characters a reader sees.
import type { API, BlockAPI, BlockTool, BlockToolConstructorOptions, ToolboxConfig } from '@editorjs/editorjs'

export interface ImageData {
  media: string
  width: number
  height: number
  caption: string
}

/** The longest caption, in characters a reader sees (Platform.Modules.Editor BlockSchemas.ImageCaptionMaxLength). */
export const imageCaptionMaxLength = 300

/** What uploading a file gave: the file's id or why it failed (api/media.ts MediaUpload, by status). */
export type ImageUpload =
  { status: 'uploaded'; media: string } | { status: 'unsupported' | 'too-large' | 'unauthorized' | 'error' }

export interface ImageToolConfig {
  upload: (file: File) => Promise<ImageUpload>
  /** The picture's size in pixels, or null when the browser cannot read it as an image. */
  measure?: (url: string) => Promise<{ width: number; height: number } | null>
  /** A link to show a picked file before it is uploaded; the browser's object URL by default. */
  preview?: (file: File) => string
}

export const imageMessages = {
  choose: 'Выбрать фото',
  chooseAnother: 'Выбрать другой файл',
  retry: 'Повторить',
  uploading: 'Загружаем фото…',
  caption: 'Подпись',
  unreadable: 'Это не изображение или файл повреждён.',
  unsupported: 'Такой формат не подходит: нужны JPEG, PNG, GIF или WebP.',
  'too-large': 'Файл больше 20 МБ.',
  unauthorized: 'Войдите, чтобы загружать фото.',
  error: 'Не удалось загрузить фото. Попробуйте ещё раз.',
} as const

const imageIcon =
  '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.1-3.1a2 2 0 0 0-2.8 0L6 21"/></svg>'

// The local previews of the files uploaded in this page, by file id: undo and redo render a block again from its
// data, and the picture must come back without asking the server.
const previews = new Map<string, string>()

/** Remembers the link that shows the uploaded file `media` (also for a post opened with its PostView.images). */
export function rememberPreview(media: string, url: string) {
  previews.set(media, url)
}

/** The characters a reader sees in a caption: no tags, entities decoded, a line break as one (InlineHtml.TextLength). */
export function captionLength(html: string): number {
  const decoded = html
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&amp;/g, '&')
  return decoded.length
}

/** The picture's size in pixels as the browser reads it, or null when it is not an image. */
export const measureInBrowser = (url: string) =>
  new Promise<{ width: number; height: number } | null>((resolve) => {
    const image = new Image()
    image.onload = () =>
      resolve(image.naturalWidth > 0 ? { width: image.naturalWidth, height: image.naturalHeight } : null)
    image.onerror = () => resolve(null)
    image.src = url
  })

type State = 'empty' | 'uploading' | 'error' | 'ready'

export class ImageTool implements BlockTool {
  static get toolbox(): ToolboxConfig {
    return { title: 'Image', icon: imageIcon }
  }

  // Enter in the caption goes on with a text block; Editor.js must not split the block on it.
  static get enableLineBreaks() {
    return true
  }

  static get isReadOnlySupported() {
    return true
  }

  private readonly api: API
  private readonly block: BlockAPI | undefined
  private readonly config: ImageToolConfig
  private readonly element: HTMLElement
  private readonly picture: HTMLImageElement
  private readonly status: HTMLElement
  private readonly actions: HTMLElement
  private readonly caption: HTMLElement
  private readonly input: HTMLInputElement
  private data: Omit<ImageData, 'caption'> | null = null
  private file: File | null = null
  private started = false

  constructor({
    data,
    api,
    block,
    config,
    readOnly,
  }: BlockToolConstructorOptions<Partial<ImageData>, ImageToolConfig>) {
    this.api = api
    this.block = block
    this.config = config ?? { upload: async () => ({ status: 'error' }) }
    this.element = document.createElement('figure')
    this.element.classList.add('ce-image')
    this.element.dataset.testid = 'image-block'
    this.element.contentEditable = 'false'

    this.picture = document.createElement('img')
    this.picture.classList.add('ce-image__picture')
    this.picture.alt = ''
    this.status = document.createElement('p')
    this.status.classList.add('ce-image__status')
    this.actions = document.createElement('div')
    this.actions.classList.add('ce-image__actions')
    this.input = document.createElement('input')
    this.input.type = 'file'
    this.input.accept = 'image/*'
    this.input.hidden = true
    this.input.dataset.testid = 'image-input'
    this.input.addEventListener('change', () => {
      const file = this.input.files?.[0]
      this.input.value = ''
      if (file) void this.uploadFile(file)
    })

    this.caption = document.createElement('figcaption')
    this.caption.classList.add('ce-image__caption')
    this.caption.dataset.placeholder = imageMessages.caption
    this.caption.contentEditable = readOnly ? 'false' : 'true'
    this.caption.innerHTML = typeof data?.caption === 'string' ? data.caption : ''
    if (!readOnly) {
      this.caption.addEventListener('beforeinput', (event) => this.limitCaption(event))
      this.caption.addEventListener('input', () => this.trimCaption())
      this.caption.addEventListener('keydown', (event) => this.onCaptionKeydown(event))
    }

    this.element.append(this.picture, this.status, this.actions, this.input, this.caption)
    if (typeof data?.media === 'string' && data.media && Number(data.width) > 0 && Number(data.height) > 0) {
      this.data = { media: data.media, width: Number(data.width), height: Number(data.height) }
      this.show('ready', previews.get(data.media))
    } else this.show('empty')
  }

  render(): HTMLElement {
    return this.element
  }

  /** Added from the block menu: the file picker opens at once, unless a file is already on its way (the phone panel). */
  rendered() {
    setTimeout(() => {
      if (!this.started && !this.data && this.element.isConnected) this.input.click()
    }, 0)
  }

  save(): ImageData {
    return {
      media: this.data?.media ?? '',
      width: this.data?.width ?? 0,
      height: this.data?.height ?? 0,
      caption: this.captionHtml(),
    }
  }

  // A block on its way or failed is kept like an empty paragraph; document.ts leaves it out of the saved document.
  validate(): boolean {
    return true
  }

  /** Shows `file` at once and uploads it; called by the file picker and by the phone panel (BlockAPI.call). */
  async uploadFile(file: File): Promise<void> {
    this.started = true
    this.file = file
    this.data = null
    const url = (this.config.preview ?? URL.createObjectURL)(file)
    this.show('uploading', url)
    const size = await (this.config.measure ?? measureInBrowser)(url)
    if (!size) return this.show('error', undefined, imageMessages.unreadable)
    const outcome = await this.config.upload(file).catch((): ImageUpload => ({ status: 'error' }))
    if (this.file !== file) return
    if (outcome.status !== 'uploaded') return this.show('error', url, imageMessages[outcome.status])
    previews.set(outcome.media, url)
    this.data = { media: outcome.media, ...size }
    this.show('ready', url)
    this.block?.dispatchChange()
  }

  private show(state: State, url?: string, message = '') {
    this.element.dataset.state = state
    this.element.setAttribute('aria-busy', state === 'uploading' ? 'true' : 'false')
    if (url) this.picture.src = url
    else this.picture.removeAttribute('src')
    this.picture.hidden = !url && state !== 'ready'
    if (this.data) {
      this.picture.width = this.data.width
      this.picture.height = this.data.height
      this.picture.style.aspectRatio = `${this.data.width} / ${this.data.height}`
    }
    this.status.replaceChildren()
    this.status.removeAttribute('role')
    if (state === 'uploading') {
      const indicator = document.createElement('span')
      indicator.classList.add('ce-image__spinner')
      indicator.setAttribute('role', 'progressbar')
      indicator.setAttribute('aria-label', imageMessages.uploading)
      this.status.append(indicator, imageMessages.uploading)
    } else if (state === 'error') {
      this.status.setAttribute('role', 'alert')
      this.status.textContent = message
    }
    this.status.hidden = state === 'ready' || state === 'empty'
    this.actions.replaceChildren()
    if (state === 'empty')
      this.actions.append(this.button(imageMessages.choose, 'image-choose', () => this.pick()))
    if (state === 'error') {
      const file = this.file
      if (file && message !== imageMessages.unreadable)
        this.actions.append(this.button(imageMessages.retry, 'image-retry', () => void this.uploadFile(file)))
      this.actions.append(this.button(imageMessages.chooseAnother, 'image-choose', () => this.pick()))
    }
    this.actions.hidden = this.actions.childElementCount === 0
    this.caption.hidden = state !== 'ready' && state !== 'uploading'
  }

  private button(label: string, testid: string, onClick: () => void): HTMLButtonElement {
    const button = document.createElement('button')
    button.type = 'button'
    button.classList.add('ce-image__button')
    button.dataset.testid = testid
    button.textContent = label
    button.addEventListener('click', onClick)
    return button
  }

  private pick() {
    this.started = true
    this.input.click()
  }

  private captionHtml(): string {
    const html = this.caption.innerHTML
    return html
      .replace(/<br\s*\/?>/gi, '')
      .replace(/&nbsp;/g, ' ')
      .trim() === ''
      ? ''
      : html
  }

  // Typing or pasting past the limit is refused; what is left over (a drop, an engine's paste) is cut by trimCaption.
  private limitCaption(event: InputEvent) {
    if (!event.inputType.startsWith('insert')) return
    const added = event.data ?? event.dataTransfer?.getData('text/plain') ?? ''
    const selection = window.getSelection()
    const selected =
      selection && this.caption.contains(selection.anchorNode) ? selection.toString().length : 0
    if (captionLength(this.captionHtml()) - selected + Math.max(added.length, 1) > imageCaptionMaxLength)
      event.preventDefault()
  }

  private trimCaption() {
    if (captionLength(this.captionHtml()) <= imageCaptionMaxLength) return
    this.caption.textContent = (this.caption.textContent ?? '').slice(0, imageCaptionMaxLength)
  }

  // The caption is one line: Enter goes on with a text block after the picture.
  private onCaptionKeydown(event: KeyboardEvent) {
    if (event.key !== 'Enter' || event.isComposing) return
    event.preventDefault()
    event.stopPropagation()
    const index = this.block
      ? this.api.blocks.getBlockIndex(this.block.id)
      : this.api.blocks.getCurrentBlockIndex()
    if (index < 0) return
    this.api.blocks.insert('paragraph', { text: '' }, undefined, index + 1, true)
    this.api.caret.setToBlock(index + 1, 'start')
  }
}
