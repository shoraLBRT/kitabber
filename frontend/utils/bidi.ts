// Right-to-left runs inside left-to-right text (E02-14, ADR-0068). A block is `dir="auto"`: one that starts with
// Arabic (or Hebrew) letters is right to left as a whole. A Russian block with an Arabic quote stays left to right,
// and the Unicode bidi algorithm then lets the Arabic run pull its neighbours in: digits after it become Arabic numbers
// and move to its left («عليكم 5 раз» reads «5 عليكم раз»), neutral punctuation between two Arabic runs joins them.
// So each right-to-left run of such a block is wrapped in RLI…PDI (U+2067, U+2069), an isolate: inside it the run is
// right to left, outside it is one neutral character, and the punctuation and digits around it keep the direction of
// the block. The marks are invisible and, unlike a <bdi> element, may cross the inline tags of the HTML (<b>, <a>).
// A run starts and ends with a right-to-left character; spaces, digits, punctuation and tags between two such
// characters belong to it, a letter of another script ends it.

/** Hebrew, Arabic, Syriac, Thaana, NKo and the Arabic extensions and presentation forms, with their marks. */
const char = (code: number) => String.fromCharCode(code)
const range = (from: number, to: number) => `${char(from)}-${char(to)}`
const rtl = new RegExp(`[${range(0x0590, 0x08ff)}${range(0xfb1d, 0xfdff)}${range(0xfe70, 0xfefc)}]`, 'u')
const letter = /\p{L}/u
const isolateStart = char(0x2067) // RIGHT-TO-LEFT ISOLATE
const isolateEnd = char(0x2069) // POP DIRECTIONAL ISOLATE

/** The pieces of a text as the bidi algorithm sees them: one character, or one HTML entity (a neutral). */
const units = (text: string, html: boolean) =>
  (html ? text.match(/&[#\w]+;|[\s\S]/gu) : Array.from(text)) ?? []

const isEntity = (unit: string, html: boolean) => html && unit.length > 1 && unit.startsWith('&')
/** A line break ends a run: the isolate never spans two lines. */
const lineBreak = new RegExp(`^(?:[\n\r${char(0x2029)}]|<br\\b[^>]*>)$`, 'u')

/** True when the first strong letter is right to left: `dir="auto"` makes the block right to left, nothing to do. */
function startsRtl(parts: readonly string[], html: boolean): boolean {
  for (const part of parts) {
    if (html && part.startsWith('<')) continue
    for (const unit of units(part, html)) {
      if (rtl.test(unit)) return true
      if (!isEntity(unit, html) && letter.test(unit)) return false
    }
  }
  return false
}

function isolate(parts: readonly string[], html: boolean): string {
  if (startsRtl(parts, html)) return parts.join('')
  let out = ''
  let open = false
  let pending = '' // what follows the last right-to-left character of an open run: it joins the run or ends up after it
  for (const part of parts) {
    if (html && part.startsWith('<')) {
      if (open && lineBreak.test(part)) {
        out += isolateEnd + pending + part
        pending = ''
        open = false
      } else if (open) pending += part
      else out += part
      continue
    }
    for (const unit of units(part, html)) {
      const entity = isEntity(unit, html)
      if (!entity && rtl.test(unit)) {
        out += open ? pending : isolateStart
        pending = ''
        open = true
        out += unit
      } else if (open && ((!entity && letter.test(unit)) || lineBreak.test(unit))) {
        out += isolateEnd + pending + unit
        pending = ''
        open = false
      } else if (open) {
        pending += unit
      } else {
        out += unit
      }
    }
  }
  return open ? out + isolateEnd + pending : out
}

/** Isolates the right-to-left runs of inline HTML already cleaned by cleanInlineHtml (tags and entities kept). */
export function isolateRtlHtml(html: string): string {
  return isolate(html.split(/(<[^>]*>)/), true)
}

/** Isolates the right-to-left runs of plain text (a comment, a title) drawn by text interpolation. */
export function isolateRtlText(text: string): string {
  return isolate([text], false)
}
