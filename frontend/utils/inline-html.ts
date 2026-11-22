// The inline HTML of a block's text drawn on the reader side (E07-03, ADR-0045): the whitelist of ADR-0023 checked a
// second time before v-html. The server already stores the text cleaned, so this pass changes nothing in a stored
// post; it keeps the renderer safe for any document it is given (a preview, a future source) and gives the markup the
// same shape on the server and in the browser. A tag outside the whitelist is dropped and its text kept, loose `<` and
// `>` are escaped, every opened tag is closed.

const plainTags = new Set(['b', 'strong', 'i', 'em', 'mark'])

/** The `rel` of every link in a post (ADR-0023). */
export const linkRel = 'nofollow ugc noopener noreferrer'

const entities: Record<string, string> = { amp: '&', lt: '<', gt: '>', quot: '"', '#39': "'", apos: "'" }

function decode(value: string): string {
  return value.replace(/&(amp|lt|gt|quot|#39|apos);/g, (_, name: string) => entities[name]!)
}

function escapeAttribute(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** The `href` of an `<a …>` tag if its scheme is allowed (http, https, mailto), otherwise null. */
function allowedHref(tag: string): string | null {
  const match = /\shref\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'>]+))/i.exec(tag)
  if (!match) return null
  const href = decode(match[1] ?? match[2] ?? match[3] ?? '').trim()
  return /^(https?|mailto):/i.test(href) ? href : null
}

export function cleanInlineHtml(html: string): string {
  const out: string[] = []
  const open: string[] = []
  // A tag is `<` and a letter (or `/` and a letter); anything else with `<` is text.
  const tokens = html.split(/(<\/?[a-zA-Z][^<>]*>)/)
  tokens.forEach((token, index) => {
    if (index % 2 === 0) {
      out.push(token.replace(/</g, '&lt;').replace(/>/g, '&gt;'))
      return
    }
    const [, slash, rawName] = /^<(\/?)([a-zA-Z][a-zA-Z0-9]*)/.exec(token)!
    const name = rawName!.toLowerCase()
    if (name === 'br') {
      if (!slash) out.push('<br>')
      return
    }
    if (!plainTags.has(name) && name !== 'a') return
    if (slash) {
      const at = open.lastIndexOf(name)
      if (at < 0) return
      while (open.length > at) out.push(`</${open.pop()}>`)
      return
    }
    if (name === 'a') {
      const href = allowedHref(token)
      // A link inside a link is not valid HTML: the browser would split it, and the server and the client would differ.
      if (href === null || open.includes('a')) return
      out.push(`<a href="${escapeAttribute(href)}" rel="${linkRel}">`)
    } else {
      out.push(`<${name}>`)
    }
    open.push(name)
  })
  while (open.length > 0) out.push(`</${open.pop()}>`)
  return out.join('')
}
