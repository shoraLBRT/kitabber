import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref, type PropType } from 'vue'
import { maxImpressions, parseViewReport, type ViewReport } from '../../api/views'
import { setCookieConsent } from '../../composables/useCookieConsent'
import {
  flushImpressions,
  impressionFlushMs,
  markImpression,
  resetImpressions,
  usePostViews,
} from '../../composables/useViews'
import { acceptAll, necessaryOnly } from '../../utils/cookie-consent'

// The view reports of the browser (E07-06, ADR-0071): what the site's server takes, the cards sent together and once,
// the opening of a post and its reading time only with the «Аналитические» consent. The API's counting rules are
// tested on the server (PostViewApiTests); both profiles against the fake API: e2e/views.spec.ts.
const id = (n: number) => `0195f3a0-0000-7000-8000-${String(n).padStart(12, '0')}`

let sent: Blob[] = []

beforeEach(() => {
  vi.useFakeTimers()
  sent = []
  resetImpressions()
  vi.stubGlobal(
    'fetch',
    vi.fn((_url: string, init: RequestInit) => {
      sent.push(new Blob([init.body as string]))
      return Promise.resolve(new Response(null, { status: 204 }))
    }),
  )
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.useRealTimers()
  setCookieConsent(necessaryOnly)
})

async function reports(): Promise<ViewReport[]> {
  return Promise.all(sent.map(async (body) => JSON.parse(await body.text()) as ViewReport))
}

describe('parseViewReport', () => {
  it('takes the reports the API takes', () => {
    expect(parseViewReport({ kind: 'open', postId: id(1) })).toEqual({ kind: 'open', postId: id(1) })
    expect(parseViewReport({ kind: 'impressions', postIds: [id(1), id(2)] })).toEqual({
      kind: 'impressions',
      postIds: [id(1), id(2)],
    })
    expect(parseViewReport({ kind: 'reading', postId: id(1), seconds: 90 })).toEqual({
      kind: 'reading',
      postId: id(1),
      seconds: 90,
    })
  })

  it('refuses anything else', () => {
    expect(parseViewReport(null)).toBeNull()
    expect(parseViewReport({ kind: 'open', postId: 'not-an-id' })).toBeNull()
    expect(parseViewReport({ kind: 'impressions', postIds: [] })).toBeNull()
    expect(
      parseViewReport({
        kind: 'impressions',
        postIds: Array.from({ length: maxImpressions + 1 }, (_, n) => id(n)),
      }),
    ).toBeNull()
    expect(parseViewReport({ kind: 'reading', postId: id(1), seconds: 0 })).toBeNull()
    expect(parseViewReport({ kind: 'reading', postId: id(1), seconds: 7201 })).toBeNull()
    expect(parseViewReport({ kind: 'reading', postId: id(1), seconds: 1.5 })).toBeNull()
    expect(parseViewReport({ kind: 'like', postId: id(1) })).toBeNull()
  })
})

describe('impressions', () => {
  it('sends the cards seen together, each once', async () => {
    markImpression(id(1))
    markImpression(id(2))
    markImpression(id(1))
    expect(await reports()).toEqual([])

    vi.advanceTimersByTime(impressionFlushMs)
    markImpression(id(2))
    flushImpressions()

    expect(await reports()).toEqual([{ kind: 'impressions', postIds: [id(1), id(2)] }])
  })

  it('splits a long list into reports of 50', async () => {
    for (let n = 0; n < maxImpressions + 3; n++) markImpression(id(n))
    flushImpressions()

    const all = await reports()
    expect(all.map((r) => (r.kind === 'impressions' ? r.postIds.length : 0))).toEqual([maxImpressions, 3])
  })
})

describe('usePostViews', () => {
  const Page = defineComponent({
    props: { postId: { type: String as PropType<string | null>, default: null } },
    setup(props) {
      usePostViews(() => props.postId ?? null)
      return () => h('div')
    },
  })

  it('reports the opening of a published post and nothing for a draft', async () => {
    const draft = mount(Page, { props: { postId: null } })
    draft.unmount()
    const page = mount(Page, { props: { postId: id(7) } })
    await nextTick()

    expect(await reports()).toEqual([{ kind: 'open', postId: id(7) }])
    page.unmount()
  })

  it('reports the reading time only with the analytics consent', async () => {
    const refused = mount(Page, { props: { postId: id(7) } })
    vi.advanceTimersByTime(30_000)
    refused.unmount()
    expect((await reports()).filter((r) => r.kind === 'reading')).toEqual([])

    setCookieConsent(acceptAll)
    const allowed = mount(Page, { props: { postId: id(8) } })
    vi.advanceTimersByTime(42_500)
    allowed.unmount()

    expect((await reports()).filter((r) => r.kind === 'reading')).toEqual([
      { kind: 'reading', postId: id(8), seconds: 42 },
    ])
  })

  it('reports the reading of the first post when the page moves to another', async () => {
    setCookieConsent(acceptAll)
    const postId = ref(id(1))
    const Moving = defineComponent({
      setup() {
        usePostViews(() => postId.value)
        return () => h('div')
      },
    })
    const page = mount(Moving)
    vi.advanceTimersByTime(5_000)
    postId.value = id(2)
    await nextTick()

    expect(await reports()).toEqual([
      { kind: 'open', postId: id(1) },
      { kind: 'reading', postId: id(1), seconds: 5 },
      { kind: 'open', postId: id(2) },
    ])
    page.unmount()
  })
})
