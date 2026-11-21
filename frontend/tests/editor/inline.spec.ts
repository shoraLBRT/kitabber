import { describe, expect, it } from 'vitest'
import { cleanInline, cleanPastedHtml, isSafeHref, linkRel, normalizeLink } from '../../editor/inline'
import { googleDocsHtml, webHtml, wordHtml } from './paste-samples'

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

  it('cleans a web page to paragraphs of allowed markup', () => {
    expect(cleanPastedHtml(webHtml)).toBe(
      '<p>Заголовок <i>статьи</i></p>' +
        `<p>Текст с <b>важным</b> и <a href="https://example.test/" rel="${linkRel}">ссылкой</a>, относительной и цветом.</p>` +
        '<p>Пункт один</p><p>Пункт <mark>два</mark></p>',
    )
  })

  it('keeps line breaks inside a paragraph and merges neighbouring runs of one format', () => {
    expect(cleanPastedHtml('<p><b>a</b><b>b</b><br>c</p>')).toBe('<p><b>ab</b><br>c</p>')
  })

  it('gives nothing for HTML without text', () => {
    expect(cleanPastedHtml('<p>&nbsp;</p><img src="x"><script>1</script>')).toBe('')
  })
})
