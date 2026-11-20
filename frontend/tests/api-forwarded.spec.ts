// Acceptance check of E01-10 (#21), the Nuxt end: API calls made during server rendering carry the client address
// the proxy gave the page, so the API's rate limits count each reader, not the Nuxt container. The API end (429 with
// Retry-After, counting by IP and by account) is checked in backend/tests (RateLimitingTests).
import { describe, expect, it } from 'vitest'
import { createApiClient } from '../api/client'
import { forwardedHeaders } from '../api/forwarded'

describe('forwardedHeaders', () => {
  it('carries the incoming x-forwarded-for on', () => {
    expect(forwardedHeaders(' 203.0.113.7, 10.0.0.2 ')).toEqual({ 'x-forwarded-for': '203.0.113.7, 10.0.0.2' })
  })

  it('adds nothing without an incoming header', () => {
    expect(forwardedHeaders(undefined)).toEqual({})
    expect(forwardedHeaders('  ')).toEqual({})
  })

  it('reaches the API request', async () => {
    const calls: Request[] = []
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      headers: forwardedHeaders('203.0.113.7'),
      fetch: async (request: Request) => {
        calls.push(request)
        return new Response('[]', { status: 200, headers: { 'content-type': 'application/json' } })
      },
    })
    const get = client.GET as unknown as (path: string) => Promise<unknown>

    await get('/api/v1/anything')

    expect(calls[0]?.headers.get('x-forwarded-for')).toBe('203.0.113.7')
  })
})
