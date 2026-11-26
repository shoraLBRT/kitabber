// The blocks of a post drawn for reading (E07-03, #51, ADR-0045): a snapshot of the server-rendered markup of every
// block type of schema version 1 (ADR-0023), the placeholder of an unknown block, and the inline HTML checked again
// against the whitelist.
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import PostBlocks from '../../components/post/PostBlocks.vue'
import { listTree, viewBlocks } from '../../utils/post-blocks'
import { cleanInlineHtml, linkRel } from '../../utils/inline-html'

const doc = (...blocks: unknown[]) => ({ version: 1, blocks })

/** The markup the server sends, without Vue's fragment comments and the scope attributes of the styles. */
async function serverHtml(document: unknown): Promise<string> {
  const html = await renderToString(createSSRApp({ render: () => h(PostBlocks, { document }) }))
  return html
    .replace(/<!--.*?-->/g, '')
    .replace(/ data-v-[a-f0-9]+/g, '')
    .replace(/></g, '>\n<')
}

describe('PostBlocks: a snapshot of each block type', () => {
  it('paragraph', async () => {
    expect(
      await serverHtml(
        doc({
          id: 'p1',
          type: 'paragraph',
          data: {
            text: 'Текст с <b>жирным</b>, <i>курсивом</i>, <mark>маркером</mark> и <a href="https://example.org/?a=1&amp;b=2" rel="nofollow ugc noopener noreferrer">ссылкой</a>.<br>Вторая строка',
          },
        }),
      ),
    ).toMatchSnapshot()
  })

  it('header of level 2 and 3 with anchors', async () => {
    expect(
      await serverHtml(
        doc(
          { id: 'h2', type: 'header', data: { text: 'Глава', level: 2 } },
          { id: 'h3', type: 'header', data: { text: 'Раздел <i>главы</i>', level: 3 } },
        ),
      ),
    ).toMatchSnapshot()
  })

  it('bulleted and numbered lists with nesting', async () => {
    expect(
      await serverHtml(
        doc(
          { id: 'l1', type: 'list', data: { style: 'unordered', items: ['Раз', 'Два'], levels: [] } },
          {
            id: 'l2',
            type: 'list',
            data: { style: 'ordered', items: ['Первый', '<b>Вложенный</b>', 'Второй'], levels: [0, 1, 0] },
          },
        ),
      ),
    ).toMatchSnapshot()
  })

  it('quote with and without a caption', async () => {
    expect(
      await serverHtml(
        doc(
          { id: 'q1', type: 'quote', data: { text: 'Краткость — сестра таланта.', caption: 'А. П. Чехов' } },
          { id: 'q2', type: 'quote', data: { text: 'Без подписи', caption: '' } },
        ),
      ),
    ).toMatchSnapshot()
  })

  it('delimiter', async () => {
    expect(await serverHtml(doc({ id: 'd1', type: 'delimiter', data: {} }))).toMatchSnapshot()
  })

  it('unknown block and an empty document', async () => {
    expect(
      await serverHtml(
        doc({ id: 'x1', type: 'gallery', data: { images: [] } }, { id: 'x2', type: 'header', data: {} }),
      ),
    ).toMatchSnapshot()
    expect(await serverHtml(doc())).toMatchSnapshot()
  })

  // E02-14 (#67, ADR-0068): every text is dir="auto"; an Arabic block is left as it is, the Arabic runs of a Russian
  // block are isolated (RLI…PDI, written here as [ and ]) so the punctuation and digits around them keep their places.
  it('Arabic and mixed Russian and Arabic text', async () => {
    const html = await serverHtml(
      doc(
        { id: 'r1', type: 'paragraph', data: { text: 'بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ، قال: «Привет»!' } },
        {
          id: 'r2',
          type: 'paragraph',
          data: { text: 'Он сказал: «<b>السلام</b> عليكم», и повторил 3 раза.' },
        },
        { id: 'r3', type: 'header', data: { text: 'Глава «كتاب»', level: 2 } },
        {
          id: 'r4',
          type: 'list',
          data: { style: 'unordered', items: ['واحد', 'Два: اثنان.'], levels: [0, 0] },
        },
        { id: 'r5', type: 'quote', data: { text: 'العلم نور', caption: 'Пословица, «مثل»' } },
      ),
    )
    expect(
      html.replaceAll(String.fromCharCode(0x2067), '[').replaceAll(String.fromCharCode(0x2069), ']'),
    ).toMatchSnapshot()
  })
})

describe('PostBlocks: semantic markup', () => {
  const wrapper = mount(PostBlocks, {
    props: {
      document: doc(
        { id: 'a', type: 'header', data: { text: 'Глава', level: 2 } },
        { id: 'b', type: 'paragraph', data: { text: 'Абзац' } },
        {
          id: 'c',
          type: 'list',
          data: { style: 'ordered', items: ['Раз', 'Под', 'Два'], levels: [0, 1, 0] },
        },
        { id: 'd', type: 'quote', data: { text: 'Цитата', caption: 'Автор' } },
        { id: 'e', type: 'delimiter', data: {} },
        { id: 'f', type: 'poll', data: {} },
      ),
    },
  })

  it('uses the elements of each block in order', () => {
    const tags = wrapper.get('[data-testid="post-blocks"]').element.children
    expect([...tags].map((element) => element.tagName.toLowerCase())).toEqual([
      'h2',
      'p',
      'ol',
      'figure',
      'hr',
      'p',
    ])
  })

  it('nests a list item in its parent and a quote in a figure with its caption', () => {
    expect(wrapper.get('ol > li:first-child > ol > li').text()).toBe('Под')
    expect(wrapper.findAll('ol > li')).toHaveLength(3)
    expect(wrapper.get('figure > blockquote').text()).toBe('Цитата')
    expect(wrapper.get('figure > figcaption').text()).toBe('Автор')
  })

  it('shows an unknown block as a placeholder note', () => {
    const unknown = wrapper.get('[data-testid="post-block-unknown"]')
    expect(unknown.attributes('role')).toBe('note')
    expect(unknown.attributes('data-block-type')).toBe('poll')
    expect(unknown.text()).toContain('не умеем показывать')
  })
})

describe('PostBlocks: an image (E05-06)', () => {
  const image = (data: Record<string, unknown>) => doc({ id: 'i1', type: 'image', data })
  const sources = [
    { url: 'http://img.test/320.webp', width: 320 },
    { url: 'http://img.test/1280.webp', width: 1280 },
  ]

  it('is a figure with the picture in its place, its sources and its cleaned caption', () => {
    const wrapper = mount(PostBlocks, {
      props: {
        document: image({
          media: 'm-1',
          width: 800,
          height: 600,
          caption: 'Закат <span class="x"><b>над</b></span> морем',
        }),
        images: { 'm-1': sources },
      },
    })

    const picture = wrapper.get('figure[data-testid="post-block-image"] > img')
    expect(picture.attributes('width')).toBe('800')
    expect(picture.attributes('height')).toBe('600')
    expect(picture.attributes('srcset')).toBe(
      'http://img.test/320.webp 320w, http://img.test/1280.webp 1280w',
    )
    expect(picture.attributes('sizes')).toBe('(max-width: 720px) 100vw, 680px')
    expect(wrapper.get('figure > figcaption').html()).toContain('Закат <b>над</b> морем')
  })

  it('without sources holds its place with a placeholder; without a size it is unknown', () => {
    const missing = mount(PostBlocks, { props: { document: image({ media: 'm-2', width: 4, height: 3 }) } })
    expect(missing.find('[data-testid="post-block-image"] > [data-state="empty"]').exists()).toBe(true)
    expect(missing.find('figcaption').exists()).toBe(false)

    const broken = mount(PostBlocks, { props: { document: image({ media: 'm-2', width: 0, height: 3 }) } })
    expect(broken.find('[data-testid="post-block-unknown"]').exists()).toBe(true)
  })
})

describe('viewBlocks', () => {
  it('reads anything that is not a document as no blocks', () => {
    for (const value of [null, undefined, 'text', {}, { blocks: 'x' }]) expect(viewBlocks(value)).toEqual([])
  })

  it('makes a block with data it cannot read unknown, and keys a block without an id by its place', () => {
    expect(
      viewBlocks(
        doc(
          { type: 'paragraph', data: { text: 1 } },
          { id: 'l', type: 'list', data: { style: 'ordered', items: [1] } },
          { id: 'q', type: 'quote' },
          null,
        ),
      ),
    ).toEqual([
      { kind: 'unknown', key: 'block-0', type: 'paragraph' },
      { kind: 'unknown', key: 'l', type: 'list' },
      { kind: 'unknown', key: 'q', type: 'quote' },
      { kind: 'unknown', key: 'block-3', type: '' },
    ])
  })

  it('reads a list without levels as all items at the top, and a level without a parent as the top', () => {
    expect(listTree(['a', 'b'], [])).toEqual([
      { html: 'a', children: [] },
      { html: 'b', children: [] },
    ])
    expect(listTree(['a', 'b', 'c'], [1, 1, 0])).toEqual([
      { html: 'a', children: [{ html: 'b', children: [] }] },
      { html: 'c', children: [] },
    ])
  })
})

describe('cleanInlineHtml', () => {
  it('keeps the whitelist of ADR-0023 as it is', () => {
    const html = `<b>b</b><strong>s</strong><i>i</i><em>e</em><mark>m</mark><br><a href="mailto:a@b.c" rel="${linkRel}">a</a> &amp; &lt;`
    expect(cleanInlineHtml(html)).toBe(html)
  })

  it('drops other tags and attributes but keeps their text', () => {
    expect(cleanInlineHtml('<span class="x" style="color:red">t</span><b onclick="x()">b</b>')).toBe(
      't<b>b</b>',
    )
    expect(cleanInlineHtml('<script>alert(1)</script><img src=x onerror=alert(1)>')).toBe('alert(1)')
  })

  it('keeps a link only with an http, https or mailto address and sets its rel', () => {
    expect(cleanInlineHtml('<a href="javascript:alert(1)">x</a>')).toBe('x')
    expect(cleanInlineHtml('<a href=" JaVaScRiPt:alert(1)">x</a>')).toBe('x')
    expect(cleanInlineHtml('<a>x</a>')).toBe('x')
    expect(cleanInlineHtml(`<a href='https://e.org/"q' target="_blank">x</a>`)).toBe(
      `<a href="https://e.org/&quot;q" rel="${linkRel}">x</a>`,
    )
    expect(cleanInlineHtml('<a href="https://a.org"><a href="https://b.org">x</a></a>')).toBe(
      `<a href="https://a.org" rel="${linkRel}">x</a>`,
    )
  })

  it('escapes loose angle brackets and closes what is left open', () => {
    expect(cleanInlineHtml('a < b > c <b>d<i>e')).toBe('a &lt; b &gt; c <b>d<i>e</i></b>')
    expect(cleanInlineHtml('<b>a<i>b</b>c</i>')).toBe('<b>a<i>b</i></b>c')
  })
})
