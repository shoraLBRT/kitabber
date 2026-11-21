// Inline markup of text fields (E05-02, ADR-0028): the same allow list as the server (ADR-0023, InlineHtml.cs) — bold,
// italic, marker, link, line break — so the editor never holds what the server would strip. Links are checked here,
// in the link tool and in pasted HTML: only http, https and mailto addresses, each link with the rel of the server.

/** The rel every link gets, as on the server (InlineHtml.LinkRel): user content is not endorsed. */
export const linkRel = 'nofollow ugc noopener noreferrer'

const schemes = new Set(['http:', 'https:', 'mailto:'])

// Elements that go with everything inside them, as on the server; the rest are unwrapped and keep their text.
const dropped = new Set([
  'script',
  'style',
  'template',
  'noscript',
  'noembed',
  'noframes',
  'iframe',
  'frameset',
  'object',
  'embed',
  'svg',
  'math',
  'textarea',
  'select',
  'option',
  'title',
  'head',
  'xmp',
  'plaintext',
  'meta',
  'link',
  'base',
  'xml',
  'img',
  'video',
  'audio',
  'canvas',
  'button',
  'input',
])

// Elements that end one paragraph and start the next when HTML is pasted.
const blocks = new Set([
  'address',
  'article',
  'aside',
  'blockquote',
  'dd',
  'div',
  'dl',
  'dt',
  'figcaption',
  'figure',
  'footer',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'header',
  'hr',
  'li',
  'main',
  'nav',
  'ol',
  'p',
  'pre',
  'section',
  'table',
  'tr',
  'ul',
])

/** True for an absolute http, https or mailto address the server keeps (a host with a dot, an address with @). */
export function isSafeHref(value: string): boolean {
  let url: URL
  try {
    url = new URL(value.trim())
  } catch {
    return false
  }
  if (!schemes.has(url.protocol)) return false
  if (url.protocol === 'mailto:') return /^[^@\s/]+@[^@\s/]+$/.test(decodeURIComponent(url.pathname))
  return url.hostname.includes('.') && !url.username && !url.password
}

/**
 * The address a user typed into the link field, ready for href, or null when it is not a link. A bare domain
 * (`example.com/page`) gets https, a bare e-mail address gets mailto; any other scheme (javascript:, data:, ftp:)
 * and anything with spaces is refused.
 */
export function normalizeLink(input: string): string | null {
  const value = input.trim()
  if (!value || /\s/.test(value)) return null
  let candidate = value
  if (!/^[a-z][a-z\d+.-]*:/i.test(value)) {
    if (/^[^@/?#]+@[^@/?#]+\.[^@/?#]+$/.test(value)) candidate = `mailto:${value}`
    else if (/^(\/\/)?[^/?#@.]+\.[^/?#@]+/.test(value)) candidate = `https://${value.replace(/^\/\//, '')}`
    else return null
  }
  return isSafeHref(candidate) ? new URL(candidate).href : null
}

interface Marks {
  href: string | null
  b: boolean
  i: boolean
  mark: boolean
}

const plain: Marks = { href: null, b: false, i: false, mark: false }

function styleOf(element: Element, property: string): string {
  const style = element.getAttribute('style') ?? ''
  const match = new RegExp(`(?:^|;)\\s*${property}\\s*:\\s*([^;]+)`, 'i').exec(style)
  return match ? match[1]!.trim().toLowerCase() : ''
}

function isBoldWeight(weight: string): boolean {
  return weight === 'bold' || weight === 'bolder' || Number(weight) >= 600
}

function isNormalWeight(weight: string): boolean {
  return weight === 'normal' || weight === 'lighter' || (weight !== '' && Number(weight) < 600)
}

// The formatting an element gives its content: tags and the inline styles Word and Google Docs use instead of tags
// (`<span style="font-weight:700">`, and the `<b style="font-weight:normal">` Google Docs wraps every copy in).
function marksOf(element: Element, outer: Marks): Marks {
  const tag = element.localName
  const weight = styleOf(element, 'font-weight')
  const fontStyle = styleOf(element, 'font-style')
  let b = outer.b
  if (tag === 'b' || tag === 'strong') b = !isNormalWeight(weight)
  else if (isBoldWeight(weight)) b = true
  else if (isNormalWeight(weight)) b = false
  let i = outer.i
  if (tag === 'i' || tag === 'em') i = fontStyle !== 'normal'
  else if (fontStyle === 'italic' || fontStyle === 'oblique') i = true
  else if (fontStyle === 'normal') i = false
  let href = outer.href
  if (tag === 'a' && !href) {
    const value = element.getAttribute('href')
    href = value && isSafeHref(value) ? value.trim() : null
  }
  // Word's text highlight is a style, not an element.
  const highlight = styleOf(element, 'mso-highlight')
  return { href, b, i, mark: outer.mark || tag === 'mark' || (highlight !== '' && highlight !== 'none') }
}

class Writer {
  readonly doc = document.implementation.createHTMLDocument('')
  readonly paragraphs: HTMLElement[] = []
  private current: HTMLElement | null = null

  constructor(private readonly collapse: boolean) {}

  /** Ends the paragraph being written; the next content starts a new one. */
  break() {
    this.current = null
  }

  private paragraph(): HTMLElement {
    if (!this.current) {
      this.current = this.doc.createElement('p')
      this.paragraphs.push(this.current)
    }
    return this.current
  }

  text(value: string, marks: Marks) {
    const text = this.collapse ? value.replace(/[ \t\r\n\f]+/g, ' ') : value
    if (!text || (this.collapse && !this.current && text === ' ')) return
    let target: Node = this.paragraph()
    // The same order of wrappers every time, so neighbouring runs merge (merge below).
    if (marks.href) target = this.wrap(target, 'a', marks.href)
    if (marks.b) target = this.wrap(target, 'b')
    if (marks.i) target = this.wrap(target, 'i')
    if (marks.mark) target = this.wrap(target, 'mark')
    target.appendChild(this.doc.createTextNode(text))
  }

  lineBreak() {
    this.paragraph().appendChild(this.doc.createElement('br'))
  }

  private wrap(parent: Node, tag: string, href?: string): Node {
    const element = this.doc.createElement(tag)
    if (href) {
      element.setAttribute('href', href)
      element.setAttribute('rel', linkRel)
    }
    parent.appendChild(element)
    return element
  }
}

function walk(node: Node, marks: Marks, writer: Writer, split: boolean) {
  for (const child of Array.from(node.childNodes)) {
    if (child.nodeType === Node.TEXT_NODE) {
      writer.text((child as Text).data, marks)
      continue
    }
    if (child.nodeType !== Node.ELEMENT_NODE) continue // comments, Word's conditional blocks among them
    const element = child as Element
    const tag = element.localName
    if (dropped.has(tag)) continue
    if (tag === 'br') {
      writer.lineBreak()
      continue
    }
    const isBlock = split && blocks.has(tag)
    if (isBlock) writer.break()
    walk(element, marksOf(element, marks), writer, split)
    if (isBlock) writer.break()
  }
}

// Joins neighbouring elements of the same kind (`<b>a</b><b>b</b>` → `<b>ab</b>`), inside out.
function merge(parent: Node) {
  let child = parent.firstChild
  while (child) {
    const next = child.nextSibling
    if (
      next &&
      child instanceof Element &&
      next instanceof Element &&
      child.localName === next.localName &&
      child.localName !== 'br' &&
      child.getAttribute('href') === next.getAttribute('href')
    ) {
      while (next.firstChild) child.appendChild(next.firstChild)
      next.remove()
      continue
    }
    merge(child)
    child = next
  }
  parent.normalize()
}

function parse(html: string): HTMLElement {
  return new DOMParser().parseFromString(html, 'text/html').body
}

function isBlank(paragraph: HTMLElement): boolean {
  // Only spaces or line breaks (Word's `<o:p>&nbsp;</o:p>`, a `<br>` between blocks): nothing to paste.
  return (paragraph.textContent ?? '').replace(/ /g, ' ').trim() === ''
}

/**
 * The inline HTML of one text field with everything outside the allow list removed: unknown elements are unwrapped,
 * scripts and the like go with their content, `strong` and `em` become `b` and `i` (the tags of the editor's tools),
 * links keep only a safe href and get rel. Text and spaces are kept as they are.
 */
export function cleanInline(html: string): string {
  if (!/[<&]/.test(html)) return html
  const writer = new Writer(false)
  walk(parse(html), plain, writer, false)
  return writer.paragraphs
    .map((paragraph) => {
      merge(paragraph)
      return paragraph.innerHTML
    })
    .join('')
}

/**
 * HTML from the clipboard (Word, Google Docs, a web page) as paragraphs of clean inline markup, `<p>…</p><p>…</p>`:
 * block elements end a paragraph, the formatting of styles becomes tags, spaces collapse as the browser shows them,
 * and empty paragraphs go.
 */
export function cleanPastedHtml(html: string): string {
  const writer = new Writer(true)
  walk(parse(html), plain, writer, true)
  return writer.paragraphs
    .filter((paragraph) => !isBlank(paragraph))
    .map((paragraph) => {
      merge(paragraph)
      // A space left at the edges of a paragraph by the source's line breaks is not text.
      const first = paragraph.firstChild
      if (first instanceof Text) first.data = first.data.replace(/^ /, '')
      const last = paragraph.lastChild
      if (last instanceof Text) last.data = last.data.replace(/ $/, '')
      return paragraph.outerHTML
    })
    .join('')
}
