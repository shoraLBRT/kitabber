import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import { loadTagFeed } from '../../api/feeds'
import { linkHashtags, parseTag, tagPath } from '../../utils/hashtags'
import { viewBlocks } from '../../utils/post-blocks'

// E11-08, ADR-0120: every hashtag of a post's text leads to the feed of the tag, whatever its case.
describe('linkHashtags', () => {
  it('turns every tag of the text into a link to its lower-case feed', () => {
    expect(linkHashtags('Про #Кино и <b>#film_2026</b>.')).toBe(
      `Про <a href="${tagPath('кино')}" class="hashtag">#Кино</a> и ` +
        '<b><a href="/tags/film_2026" class="hashtag">#film_2026</a></b>.',
    )
  })

  it('leaves alone what is not a tag: a word glued before it, digits only, an entity, a link', () => {
    const html =
      'site.ru/page#part #2026 ## &#39;x&#39; &amp;#amp <a href="https://a.test" rel="x">#внутри</a> ' +
      `#${'а'.repeat(65)}`
    expect(linkHashtags(html)).toBe(html)
  })

  it('is applied to the blocks a post is drawn from', () => {
    const blocks = viewBlocks({
      version: 1,
      blocks: [{ id: 'p', type: 'paragraph', data: { text: 'О #Кино' } }],
    })
    expect(blocks[0]).toMatchObject({ kind: 'paragraph', html: expect.stringContaining('href="/tags/') })
  })
})

describe('parseTag', () => {
  it('reads a tag in any case with or without its hash, and nothing that is not a tag', () => {
    expect(parseTag('КиНо')).toBe('кино')
    expect(parseTag('#film_2026')).toBe('film_2026')
    expect(parseTag('2026')).toBeNull()
    expect(parseTag('два слова')).toBeNull()
    expect(parseTag('')).toBeNull()
  })
})

describe('loadTagFeed', () => {
  function clientAnswering(status: number, body: unknown) {
    const fetch = vi.fn(
      async () =>
        new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }),
    )
    return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
  }

  it('reads a page of the tag with the cursor of the previous page', async () => {
    const page = { items: [], nextCursor: null }
    const { client, fetch } = clientAnswering(200, page)
    expect(await loadTagFeed(client, 'кино', 'eyJhIjoxfQ')).toEqual({ status: 'found', page })
    expect((fetch.mock.calls[0] as unknown as [Request])[0].url).toBe(
      'http://api.test/api/v1/feeds/tags/%D0%BA%D0%B8%D0%BD%D0%BE?cursor=eyJhIjoxfQ',
    )
  })

  it('is an error when the word is not a tag or the API fails', async () => {
    expect(await loadTagFeed(clientAnswering(400, { code: 'validation_failed' }).client, '2026')).toEqual({
      status: 'error',
    })
    expect(await loadTagFeed(clientAnswering(500, {}).client, 'кино')).toEqual({ status: 'error' })
  })
})
