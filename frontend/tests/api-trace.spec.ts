// Acceptance check of E01-09 (#20), the Nuxt end: every API call carries a W3C `traceparent`, and during server
// rendering all calls of one page share the trace id of the incoming request. The API end (the same trace id down
// to the database) is checked in backend/tests (ObservabilityTests, ObservabilityIntegrationTests).
import { describe, expect, it } from 'vitest'
import { createApiClient } from '../api/client'
import { childTraceparent, pageTrace, parseTraceparent } from '../api/trace'

const incoming = '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01'

function recordingClient(trace?: Parameters<typeof createApiClient>[1]) {
  const calls: Request[] = []
  const client = createApiClient(
    {
      baseUrl: 'https://api.example.test',
      fetch: async (request: Request) => {
        calls.push(request)
        return new Response('[]', { status: 200, headers: { 'content-type': 'application/json' } })
      },
    },
    trace,
  )
  // The snapshot has no paths yet, so the call is untyped here; real calls are checked by the compiler.
  const get = client.GET as unknown as (path: string) => Promise<unknown>
  return { calls, get }
}

describe('trace context', () => {
  it('reads a valid traceparent and rejects malformed or all-zero ones', () => {
    expect(parseTraceparent(incoming)).toEqual({ traceId: '4bf92f3577b34da6a3ce929d0e0e4736', flags: '01' })
    expect(parseTraceparent(undefined)).toBeUndefined()
    expect(parseTraceparent('garbage')).toBeUndefined()
    expect(parseTraceparent('00-00000000000000000000000000000000-00f067aa0ba902b7-01')).toBeUndefined()
    expect(parseTraceparent('00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01')).toBeUndefined()
  })

  it('continues the incoming trace of a page, or starts a new sampled one', () => {
    expect(pageTrace(incoming).traceId).toBe('4bf92f3577b34da6a3ce929d0e0e4736')
    const fresh = pageTrace(undefined)
    expect(fresh.traceId).toMatch(/^[0-9a-f]{32}$/)
    expect(fresh.flags).toBe('01')
  })

  it('gives each call a new span id in the same trace', () => {
    const trace = pageTrace(incoming)
    const first = parseTraceparent(childTraceparent(trace))
    const second = childTraceparent(trace)
    expect(first?.traceId).toBe(trace.traceId)
    expect(second.split('-')[2]).not.toBe(childTraceparent(trace).split('-')[2])
  })
})

describe('API client tracing', () => {
  it('sends every call of a server-rendered page under the trace id of the page', async () => {
    const trace = pageTrace(incoming)
    const { calls, get } = recordingClient(() => trace)

    await get('/api/v1/one')
    await get('/api/v1/two')

    const headers = calls.map((request) => parseTraceparent(request.headers.get('traceparent')))
    expect(headers.map((h) => h?.traceId)).toEqual([trace.traceId, trace.traceId])
    expect(calls[0]!.headers.get('traceparent')).not.toBe(calls[1]!.headers.get('traceparent'))
  })

  it('starts a new trace for each call in the browser', async () => {
    const { calls, get } = recordingClient()

    await get('/api/v1/one')
    await get('/api/v1/two')

    const [first, second] = calls.map((request) => parseTraceparent(request.headers.get('traceparent')))
    expect(first?.traceId).toMatch(/^[0-9a-f]{32}$/)
    expect(second?.traceId).not.toBe(first?.traceId)
  })
})
