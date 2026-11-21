// Inline tools of the text block (E05-02, ADR-0028) for the floating panel of Editor.js: the marker and the link.
// Bold and italic are the tools built into Editor.js (`<b>`, `<i>`). The link replaces the built-in one, which takes
// any address and writes `target="_blank"`: this one accepts only what normalizeLink accepts and writes the rel of
// the server.
import type { API, InlineTool, InlineToolConstructorOptions, SanitizerConfig } from '@editorjs/editorjs'
import { isSafeHref, linkRel, normalizeLink } from './inline'

const activeClass = 'ce-inline-tool--active'

const icon = (paths: string) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`

const markerIcon = icon(
  '<path d="m9 11-6 6v3h9l3-3"/><path d="m22 12-4.6 4.6a2 2 0 0 1-2.8 0l-5.2-5.2a2 2 0 0 1 0-2.8L14 4"/>',
)
const linkIcon = icon(
  '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>',
)

/** Error under the link field when the address is not a link. */
export const linkError = 'Это не ссылка. Подойдут адреса http://, https:// и mailto:'

function toolButton(svg: string, name: string): HTMLButtonElement {
  const button = document.createElement('button')
  button.type = 'button'
  button.classList.add('ce-inline-tool')
  button.innerHTML = svg
  button.dataset.tool = name
  return button
}

function unwrap(element: HTMLElement) {
  const parent = element.parentNode
  if (!parent) return
  while (element.firstChild) parent.insertBefore(element.firstChild, element)
  element.remove()
}

/** Marker: wraps the selection in `<mark>`, or takes the marker off when the selection is in one. */
export class MarkerTool implements InlineTool {
  static isInline = true
  static title = 'Marker'
  static get sanitize(): SanitizerConfig {
    return { mark: {} }
  }

  private readonly api: API
  private button: HTMLButtonElement | null = null

  constructor({ api }: InlineToolConstructorOptions) {
    this.api = api
  }

  render(): HTMLElement {
    this.button = toolButton(markerIcon, 'marker')
    return this.button
  }

  surround(range: Range | null) {
    if (!range) return
    const mark = this.api.selection.findParentTag('MARK')
    if (mark) {
      unwrap(mark)
      return
    }
    const element = document.createElement('mark')
    element.appendChild(range.extractContents())
    // A marker inside the selection is taken off, so markers do not nest.
    element.querySelectorAll('mark').forEach((inner) => unwrap(inner as HTMLElement))
    range.insertNode(element)
    this.api.selection.expandToTag(element)
  }

  checkState(): boolean {
    const active = !!this.api.selection.findParentTag('MARK')
    this.button?.classList.toggle(activeClass, active)
    return active
  }
}

/**
 * Link: the field opens under the buttons; Enter applies the address when normalizeLink accepts it, otherwise the
 * field shows linkError and the text stays as it was. An empty field removes the link; the button on a link
 * removes it too.
 */
export class LinkTool implements InlineTool {
  static isInline = true
  static title = 'Link'
  static get sanitize(): SanitizerConfig {
    return { a: { href: (value: string) => isSafeHref(value), rel: true } } as unknown as SanitizerConfig
  }

  private readonly api: API
  private button: HTMLButtonElement | null = null
  private input: HTMLInputElement | null = null
  private error: HTMLElement | null = null
  private opened = false
  private faked = false

  constructor({ api }: InlineToolConstructorOptions) {
    this.api = api
  }

  get shortcut() {
    return 'CMD+K'
  }

  render(): HTMLElement {
    this.button = toolButton(linkIcon, 'link')
    return this.button
  }

  renderActions(): HTMLElement {
    const wrapper = document.createElement('div')
    wrapper.classList.add('link-tool')
    this.input = document.createElement('input')
    this.input.type = 'url'
    this.input.placeholder = 'Вставьте ссылку'
    this.input.enterKeyHint = 'done'
    this.input.setAttribute('aria-label', 'Адрес ссылки')
    this.input.classList.add('ce-inline-tool-input', 'link-tool__input')
    this.input.dataset.testid = 'link-input'
    this.input.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') this.apply(event)
    })
    this.input.addEventListener('input', () => this.showError(false))
    this.error = document.createElement('p')
    this.error.classList.add('link-tool__error')
    this.error.setAttribute('role', 'alert')
    this.error.dataset.testid = 'link-error'
    this.error.hidden = true
    wrapper.append(this.input, this.error)
    return wrapper
  }

  surround(range: Range | null) {
    if (range) {
      if (this.opened) this.restoreSelection()
      else this.saveSelection()
      const link = this.api.selection.findParentTag('A')
      if (link) {
        if (this.opened) {
          this.close(false)
          this.checkState()
        } else {
          // The button on a link removes it.
          this.api.selection.expandToTag(link)
          document.execCommand('unlink')
          this.close()
          this.checkState()
          this.api.inlineToolbar.close()
        }
        return
      }
    }
    if (this.opened) this.close(false)
    else this.open(true)
  }

  checkState(): boolean {
    const link = this.api.selection.findParentTag('A')
    this.button?.classList.toggle(activeClass, !!link)
    if (link && this.input) {
      this.open()
      this.input.defaultValue = link.getAttribute('href') ?? ''
      this.saveSelection()
    }
    return !!link
  }

  clear() {
    this.close()
  }

  private saveSelection() {
    this.api.selection.setFakeBackground()
    this.api.selection.save()
    this.faked = true
  }

  private restoreSelection() {
    this.api.selection.restore()
    this.api.selection.removeFakeBackground()
    this.faked = false
  }

  private open(focus = false) {
    this.input?.classList.add('ce-inline-tool-input--showed')
    if (focus) this.input?.focus()
    this.opened = true
  }

  private close(restore = true) {
    if (this.faked && restore) this.restoreSelection()
    this.input?.classList.remove('ce-inline-tool-input--showed')
    if (this.input) this.input.value = ''
    this.showError(false)
    this.opened = false
  }

  private showError(visible: boolean) {
    if (!this.error) return
    this.error.textContent = visible ? linkError : ''
    this.error.hidden = !visible
    this.input?.setAttribute('aria-invalid', String(visible))
  }

  private apply(event: KeyboardEvent) {
    const value = this.input?.value ?? ''
    event.preventDefault()
    event.stopPropagation()
    if (!value.trim()) {
      this.restoreSelection()
      document.execCommand('unlink')
      this.close()
      return
    }
    const href = normalizeLink(value)
    if (!href) {
      this.showError(true)
      return
    }
    this.restoreSelection()
    const existing = this.api.selection.findParentTag('A')
    if (existing) this.api.selection.expandToTag(existing)
    document.execCommand('createLink', false, href)
    const link = this.api.selection.findParentTag('A')
    if (link) {
      link.setAttribute('rel', linkRel)
      link.removeAttribute('target')
    }
    window.getSelection()?.collapseToEnd()
    this.close(false)
    this.api.inlineToolbar.close()
  }
}
