import { describe, expect, it } from 'vitest'
import { cleanInline, cleanPastedHtml, isSafeHref, linkRel, normalizeLink } from '../../editor/inline'
import { googleDocsHtml, googleDocsListHtml, webHtml, wordHtml, wordListHtml } from './paste-samples'

// Inline markup of text fields (E05-02, #37): the allow list of the server (ADR-0023), checked links, and HTML
// pasted from Word and web pages cleaned to it.

describe('normalizeLink', () => {
  it.each([
    ['https://example.test/page?q=1', 'https://example.test/page?q=1'],
    ['http://example.test', 'http://example.test/'],
    ['  example.test/page  ', 'https://example.test/page'],
    ['//example.test/x', 'https://example.test/x'],
    ['mailto:someone@example.test', 'mailto:someone@example.test'],
    ['someone@example.test', 'mailto:someone@example.test'],
    ['HTTPS://Example.TEST/A', 'https://example.test/A'],
  ])('accepts %s as %s', (input, href) => {
    expect(normalizeLink(input)).toBe(href)
  })

  it.each([
    '',
    '   ',
    'javascript:alert(1)',
    'JaVaScRiPt:alert(1)',
    'data:text/html,<script>alert(1)</script>',
    'ftp://example.test/file',
    'vbscript:msgbox',
    'not a link',
    'просто слово',
    'localhost:3000',
    'https://nodot/',
    'https://user:pass@example.test/',
    '/relative/path',
    '#anchor',
    'mailto:',
  ])('refuses %j', (input) => {
    expect(normalizeLink(input)).toBeNull()
  })

  it('accepts only absolute http, https and mailto addresses as safe', () => {
    expect(isSafeHref('https://example.test/')).toBe(true)
    expect(isSafeHref('mailto:a@example.test')).toBe(true)
    expect(isSafeHref('example.test')).toBe(false)
    expect(isSafeHref('javascript:alert(1)')).toBe(false)
  })
})

describe('cleanInline', () => {
  it('keeps the allowed markup as it is', () => {
    const html = `Текст <b>жирный</b> <i>курсив</i> <mark>маркер</mark><br>и <a href="https://example.test/" rel="${linkRel}">ссылка</a>&nbsp;`
    expect(cleanInline(html)).toBe(html)
  })

  it('gives every link the rel of the server and keeps only its href', () => {
    expect(
      cleanInline('<a href="https://example.test/x" target="_blank" class="c" onclick="x()">x</a>'),
    ).toBe(`<a href="https://example.test/x" rel="${linkRel}">x</a>`)
  })

  it('unwraps a link with an address that is not http, https or mailto', () => {
    expect(cleanInline('a <a href="javascript:alert(1)">b</a> <a href="/rel">c</a> <a>d</a>')).toBe('a b c d')
  })

  it('removes scripts and styles with their content and unwraps unknown elements', () => {
    expect(
      cleanInline('<span class="x">a</span><script>alert(1)</script><style>p{}</style><u>b</u><img src=x>'),
    ).toBe('ab')
  })

  it('writes strong and em as b and i, the tags of the editor tools', () => {
    expect(cleanInline('<strong>a</strong> <em>b</em>')).toBe('<b>a</b> <i>b</i>')
  })

  it('leaves text without markup untouched, spaces included', () => {
    expect(cleanInline('  два  пробела ')).toBe('  два  пробела ')
    expect(cleanInline('a &lt; b &amp; c')).toBe('a &lt; b &amp; c')
  })
})

describe('cleanPastedHtml', () => {
  it('cleans what Word puts on the clipboard', () => {
    expect(cleanPastedHtml(wordHtml)).toBe(
      '<p><b>Заголовок отчёта</b></p>' +
        '<p>Обычный текст, <i>курсив</i> и <b>жирный стилем</b>, <mark>выделение</mark>.</p>' +
        `<p>Ссылка <a href="https://example.test/report?id=1&amp;x=2" rel="${linkRel}">на отчёт</a> и опасная.</p>`,
    )
  })

  it('cleans what Google Docs puts on the clipboard, without making it all bold', () => {
    expect(cleanPastedHtml(googleDocsHtml)).toBe(
      '<p>Простой текст и <b>жирный</b><i> курсив</i></p><p>Второй абзац</p>',
    )
  })

  it('cleans a web page to paragraphs and lists of allowed markup', () => {
    expect(cleanPastedHtml(webHtml)).toBe(
      '<p>Заголовок <i>статьи</i></p>' +
        `<p>Текст с <b>важным</b> и <a href="https://example.test/" rel="${linkRel}">ссылкой</a>, относительной и цветом.</p>` +
        '<ul><li>Пункт один</li><li>Пункт <mark>два</mark></li></ul>',
    )
  })

  it('keeps line breaks inside a paragraph and merges neighbouring runs of one format', () => {
    expect(cleanPastedHtml('<p><b>a</b><b>b</b><br>c</p>')).toBe('<p><b>ab</b><br>c</p>')
  })

  it('gives nothing for HTML without text', () => {
    expect(cleanPastedHtml('<p>&nbsp;</p><img src="x"><script>1</script>')).toBe('')
  })

  it('turns the list paragraphs of Word into lists without their markers, a nested item marked by its level', () => {
    expect(cleanPastedHtml(wordListHtml)).toBe(
      '<ul><li>Первый <b>пункт</b></li><li data-level="1">Вложенный</li><li>Второй</li></ul>' +
        '<p>Между списками</p>' +
        '<ol><li>Шаг один</li><li>Шаг <i>два</i></li></ol>',
    )
  })

  it('turns a list of Google Docs into one list, the nested list in it one level down', () => {
    expect(cleanPastedHtml(googleDocsListHtml)).toBe(
      '<ol><li>Раз</li><li data-level="1"><b>Вложенный</b></li><li>Два</li></ol>',
    )
  })

  it('keeps nested lists of a web page one level down, deeper ones too, in the kind of the top list', () => {
    expect(
      cleanPastedHtml(
        '<ul><li>a<ol><li>b<ul><li>c</li></ul></li></ol></li><li>d</li></ul><ol><li>e</li></ol>',
      ),
    ).toBe(
      '<ul><li>a</li><li data-level="1">b</li><li data-level="1">c</li><li>d</li></ul><ol><li>e</li></ol>',
    )
  })

  it('keeps the paragraphs of one item in the item on lines of their own', () => {
    expect(cleanPastedHtml('<ul><li><p>a</p><p>b</p></li></ul>')).toBe('<ul><li>a<br>b</li></ul>')
  })

  it('takes an item of Word without a number for a bullet, and one with a letter or a roman number for a number', () => {
    const item = (marker: string) =>
      `<p style="mso-list:l0 level1 lfo1"><span style="mso-list:Ignore">${marker}&nbsp;</span>x</p>`
    expect(cleanPastedHtml(item('§'))).toBe('<ul><li>x</li></ul>')
    expect(cleanPastedHtml(item('-'))).toBe('<ul><li>x</li></ul>')
    expect(cleanPastedHtml(item('b)'))).toBe('<ol><li>x</li></ol>')
    expect(cleanPastedHtml(item('iv.'))).toBe('<ol><li>x</li></ol>')
  })
})
