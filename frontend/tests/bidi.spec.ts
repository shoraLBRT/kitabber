// Arabic text and right-to-left writing in content (E02-14, #67, ADR-0068): the right-to-left runs of a left-to-right
// block are isolated, a block that starts with Arabic is left as it is (dir="auto" makes it right to left), and the
// Arabic face with harakat is declared for the Arabic letters only and stands first in the families of the text.
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import tokens from '../../brand/tokens.json'
import { isolateRtlHtml, isolateRtlText } from '../utils/bidi'

// The marks written out, so the expectations show where each isolate opens and closes.
const RLI = String.fromCharCode(0x2067)
const PDI = String.fromCharCode(0x2069)
const app = join(dirname(fileURLToPath(import.meta.url)), '..')
const marks = (text: string) => text.replaceAll(RLI, '[').replaceAll(PDI, ']')

describe('isolateRtlText', () => {
  it('isolates an Arabic quote in a Russian sentence and keeps the punctuation around it outside', () => {
    expect(marks(isolateRtlText('Он сказал: «السلام عليكم», и ушёл.'))).toBe(
      'Он сказал: «[السلام عليكم]», и ушёл.',
    )
  })

  it('keeps digits after the Arabic run out of it, so they do not move to its left', () => {
    expect(marks(isolateRtlText('Повторите «سبحان الله» 33 раза.'))).toBe('Повторите «[سبحان الله]» 33 раза.')
  })

  it('takes the spaces, digits and punctuation between Arabic words into the run, harakat included', () => {
    expect(marks(isolateRtlText('Аят: بِسْمِ اللَّهِ، الرَّحْمَٰنِ 1 الرَّحِيمِ — конец'))).toBe(
      'Аят: [بِسْمِ اللَّهِ، الرَّحْمَٰنِ 1 الرَّحِيمِ] — конец',
    )
  })

  it('makes two runs of two Arabic quotes split by Russian words', () => {
    expect(marks(isolateRtlText('Слово كتاب и слово قلم.'))).toBe('Слово [كتاب] и слово [قلم].')
  })

  it('closes the run at the end of the text and at a line break', () => {
    expect(marks(isolateRtlText('Цитата: مرحبا'))).toBe('Цитата: [مرحبا]')
    expect(marks(isolateRtlText('Цитата: مرحبا\nمرحبا'))).toBe('Цитата: [مرحبا]\n[مرحبا]')
  })

  it('leaves a text that starts with Arabic, plain Russian and Hebrew-first text as they are', () => {
    for (const text of ['قال: «Привет», ثم ذهب.', 'Просто текст, 42.', 'שלום, мир', '', '123 «»']) {
      expect(isolateRtlText(text)).toBe(text)
    }
  })
})

describe('isolateRtlHtml', () => {
  it('lets the run cross inline tags and keeps tags and entities after its last Arabic letter outside it', () => {
    expect(
      marks(
        isolateRtlHtml(
          'Текст <b>السلام</b> <i>عليكم</i>&nbsp;и <a href="https://example.org/a-b">ссылка</a>',
        ),
      ),
    ).toBe('Текст <b>[السلام</b> <i>عليكم]</i>&nbsp;и <a href="https://example.org/a-b">ссылка</a>')
  })

  it('ignores the attributes of the tags and closes the run at <br>', () => {
    expect(marks(isolateRtlHtml('<a href="https://example.org/مرحبا">Ссылка</a> قلم<br>قلم'))).toBe(
      '<a href="https://example.org/مرحبا">Ссылка</a> [قلم]<br>[قلم]',
    )
  })

  it('leaves a block whose first letter is Arabic as it is, behind tags and entities', () => {
    const html = '<b>&nbsp;السلام</b> и мир'
    expect(isolateRtlHtml(html)).toBe(html)
  })
})

describe('the Arabic face (ADR-0068)', () => {
  const css = readFileSync(join(app, 'assets', 'fonts.css'), 'utf8')
  const config = readFileSync(join(app, 'nuxt.config.ts'), 'utf8')

  it('is Noto Naskh Arabic of 400 and 700, served from the site, for the Arabic block with its harakat', () => {
    const faces = css.match(/@font-face\s*\{[^}]*\}/g) ?? []
    expect(faces).toHaveLength(2)
    for (const [index, face] of faces.entries()) {
      expect(face).toContain("font-family: 'Noto Naskh Arabic'")
      expect(face).toContain(`font-weight: ${[400, 700][index]}`)
      expect(face).toContain('font-display: swap')
      expect(face).toMatch(/src: url\('@fontsource\/noto-naskh-arabic\/files\/[^']+\.woff2'\)/)
      // U+0600–U+06FF holds the letters and the harakat (U+064B–U+0652, U+0670); no Latin or Cyrillic range, so a
      // page without Arabic letters never fetches the file.
      expect(face).toMatch(/unicode-range:\s*U\+0600-06FF,/)
      expect(face).not.toMatch(/U\+0000|U\+0400/)
    }
  })

  it('stands first in the sans and serif families, and the app loads its stylesheet', () => {
    expect(tokens.font.family.sans.startsWith("'Noto Naskh Arabic', ")).toBe(true)
    expect(tokens.font.family.serif.startsWith("'Noto Naskh Arabic', ")).toBe(true)
    expect(config).toContain("'~/assets/fonts.css'")
  })
})
