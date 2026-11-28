// The realtime channel of the site (E01-11, #25, ADR-0019) on a fake SignalR connection: a fresh hub ticket on every
// connect, reconnecting after a break without end and silently, the page's topics subscribed again, no reconnect
// once signed out. The hub itself is checked in backend/tests (RealtimeIntegrationTests), the browser end to end in
// e2e/realtime.spec.ts.
import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../api/client'
import { requestRealtimeTicket } from '../api/realtime'
import {
  RealtimeChannel,
  maxRetryDelay,
  receiveMethod,
  retryDelay,
  type HubLike,
  type RealtimeEvent,
  type RetryPolicy,
} from '../realtime/channel'

class FakeHub implements HubLike {
  starts = 0
  failNextStarts = 0
  invoked: [string, unknown][] = []
  stopped = false
  private receive?: (event: RealtimeEvent) => void
  private reconnected?: () => void
  private closed?: (error?: Error) => void

  constructor(
    private readonly accessTokenFactory: () => Promise<string>,
    readonly retry: RetryPolicy,
    readonly tickets: string[],
  ) {}

  async start() {
    this.starts++
    this.tickets.push(await this.accessTokenFactory())
    if (this.failNextStarts > 0) {
      this.failNextStarts--
      throw new Error('connection refused')
    }
  }

  async stop() {
    this.stopped = true
  }

  async invoke(method: string, ...args: unknown[]) {
    this.invoked.push([method, args[0]])
  }

  on(method: string, handler: (event: RealtimeEvent) => void) {
    expect(method).toBe(receiveMethod)
    this.receive = handler
  }

  onreconnected(callback: () => void) {
    this.reconnected = callback
  }

  onclose(callback: (error?: Error) => void) {
    this.closed = callback
  }

  /** What SignalR does on a break: asks the policy, takes a new ticket, then reports the reconnect. */
  async breakAndReconnect() {
    this.invoked = []
    expect(this.retry.nextRetryDelayInMilliseconds({ previousRetryCount: 0 })).not.toBeNull()
    this.tickets.push(await this.accessTokenFactory())
    this.reconnected?.()
  }

  close() {
    this.closed?.(new Error('gave up'))
  }

  push(event: RealtimeEvent) {
    this.receive?.(event)
  }
}

function setup(ticket: () => Promise<string | null>) {
  const tickets: string[] = []
  const timers: { callback: () => void; ms: number }[] = []
  let hub: FakeHub | undefined
  const channel = new RealtimeChannel({
    ticket,
    connect: ({ accessTokenFactory, retry }) => (hub = new FakeHub(accessTokenFactory, retry, tickets)),
    setTimer: (callback, ms) => timers.push({ callback, ms }),
    clearTimer: () => {},
  })
  return { channel, tickets, timers, hub: () => hub! }
}

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

function counter() {
  let n = 0
  return async () => `ticket-${++n}`
}

describe('realtime channel', () => {
  it('connects with a ticket and asks for a new one on every reconnect', async () => {
    const { channel, tickets, hub } = setup(counter())
    channel.start()
    await flush()
    expect(channel.isConnected).toBe(true)

    await hub().breakAndReconnect()
    await hub().breakAndReconnect()

    expect(tickets).toEqual(['ticket-1', 'ticket-2', 'ticket-3'])
  })

  it('never stops reconnecting while signed in: quickly first, then every 30 seconds', () => {
    expect([0, 1, 2, 3].map(retryDelay)).toEqual([0, 2_000, 5_000, 10_000])
    expect(retryDelay(4)).toBe(maxRetryDelay)
    expect(retryDelay(1_000)).toBe(maxRetryDelay)
  })

  it('subscribes to the page topics again after a reconnect', async () => {
    const { channel, hub } = setup(counter())
    channel.start()
    await flush()
    await channel.subscribe('post:1')
    await channel.subscribe('post:2')
    await channel.unsubscribe('post:2')

    await hub().breakAndReconnect()
    await flush()

    expect(hub().invoked).toEqual([['Subscribe', 'post:1']])
  })

  it('keeps a topic two listeners share until both take it back', async () => {
    const { channel, hub } = setup(counter())
    channel.start()
    await flush()
    await channel.subscribe('post:1')
    await channel.subscribe('post:1')
    await channel.unsubscribe('post:1')
    expect(hub().invoked).toEqual([['Subscribe', 'post:1']])

    await channel.unsubscribe('post:1')
    expect(hub().invoked).toEqual([
      ['Subscribe', 'post:1'],
      ['Unsubscribe', 'post:1'],
    ])
  })

  it('retries the first connect and starts over when SignalR gives up', async () => {
    const { channel, timers, hub } = setup(counter())
    channel.start()
    hub().failNextStarts = 2
    await flush()
    expect(channel.isConnected).toBe(false)
    expect(timers.map((t) => t.ms)).toEqual([0])

    timers.shift()!.callback()
    await flush()
    expect(timers.map((t) => t.ms)).toEqual([2_000])
    timers.shift()!.callback()
    await flush()
    expect(channel.isConnected).toBe(true)
    expect(hub().starts).toBe(3)

    hub().close()
    expect(channel.isConnected).toBe(false)
    timers.shift()!.callback()
    await flush()
    expect(channel.isConnected).toBe(true)
  })

  it('stops reconnecting once the person is signed out', async () => {
    let signedIn = true
    const { channel, timers, hub } = setup(async () => (signedIn ? 'ticket' : null))
    channel.start()
    await flush()

    signedIn = false
    await expect(hub().breakAndReconnect()).rejects.toThrow('Signed out')
    expect(hub().retry.nextRetryDelayInMilliseconds({ previousRetryCount: 1 })).toBeNull()
    hub().close()
    expect(timers).toEqual([])
  })

  it('tells the reconnect handlers after a break, not on the first connect (E12-04)', async () => {
    const { channel, hub, timers } = setup(counter())
    const reconnected = vi.fn()
    const off = channel.onReconnect(reconnected)
    channel.start()
    await flush()
    expect(reconnected).not.toHaveBeenCalled()

    await hub().breakAndReconnect()
    expect(reconnected).toHaveBeenCalledTimes(1)

    // SignalR gave up and the channel started over by itself: that is a reconnect too.
    hub().close()
    timers.shift()!.callback()
    await flush()
    expect(reconnected).toHaveBeenCalledTimes(2)

    off()
    await hub().breakAndReconnect()
    expect(reconnected).toHaveBeenCalledTimes(2)
  })

  it('gives events to the handlers of their topic', async () => {
    const { channel, hub } = setup(counter())
    const all = vi.fn()
    const post = vi.fn()
    channel.on(all)
    const off = channel.on(post, 'post:1')
    channel.start()
    await flush()

    hub().push({ topic: 'post:1', type: 'comment.created', data: { id: 1 } })
    hub().push({ topic: 'account:a', type: 'notification.created', data: null })
    off()
    hub().push({ topic: 'post:1', type: 'comment.created', data: { id: 2 } })

    expect(all).toHaveBeenCalledTimes(3)
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith({ topic: 'post:1', type: 'comment.created', data: { id: 1 } })
  })

  it('stop closes the connection', async () => {
    const { channel, hub } = setup(counter())
    channel.start()
    await flush()
    await channel.stop()
    expect(hub().stopped).toBe(true)
    expect(channel.isConnected).toBe(false)
  })
})

describe('hub ticket from the API', () => {
  it('posts with the access token the server gives the client and returns the ticket', async () => {
    const calls: Request[] = []
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      headers: { authorization: 'Bearer token' },
      fetch: async (request: Request) => {
        calls.push(request)
        return Response.json({ ticket: 't', expiresAt: '2026-11-20T00:01:00Z' })
      },
    })

    expect(await requestRealtimeTicket(client)).toEqual({ ticket: 't', expiresAt: '2026-11-20T00:01:00Z' })
    expect(calls[0]!.method).toBe('POST')
    expect(calls[0]!.url).toBe('https://api.example.test/api/v1/realtime/tickets')
    expect(calls[0]!.headers.get('authorization')).toBe('Bearer token')
  })

  it('returns null when the API refuses the token', async () => {
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetch: async () =>
        new Response(JSON.stringify({ status: 401, code: 'unauthorized' }), {
          status: 401,
          headers: { 'content-type': 'application/problem+json' },
        }),
    })

    expect(await requestRealtimeTicket(client)).toBeNull()
  })
})
