// The realtime channel of the site (E01-11, ADR-0019), without Nuxt or SignalR in it, so the unit tests drive it with
// a fake connection (tests/realtime.spec.ts); plugins/realtime.client.ts gives it the real ones. The connection opens
// with a hub ticket from the site's server (never the access token, ADR-0018), asks for a fresh ticket on every
// (re)connect, keeps reconnecting after any break — silently, as docs/ui/comments.md and notifications.md want —
// and subscribes again to the topics the page listens to. It gives up only when the person is signed out.

/** An event as the hub sends it (RealtimeEvent in backend/src/Realtime). */
export interface RealtimeEvent {
  topic: string
  type: string
  data: unknown
}

export type RealtimeHandler = (event: RealtimeEvent) => void

/** The retry policy SignalR asks before each reconnect attempt; null stops reconnecting. */
export interface RetryPolicy {
  nextRetryDelayInMilliseconds(context: { previousRetryCount: number }): number | null
}

/** The part of a SignalR HubConnection the channel uses. */
export interface HubLike {
  start(): Promise<void>
  stop(): Promise<void>
  invoke(method: string, ...args: unknown[]): Promise<unknown>
  on(method: string, handler: (event: RealtimeEvent) => void): void
  onreconnected(callback: () => void): void
  onclose(callback: (error?: Error) => void): void
}

export interface ChannelOptions {
  /** Builds the connection; it calls `accessTokenFactory` for every connect and reconnect. */
  connect(options: { accessTokenFactory: () => Promise<string>; retry: RetryPolicy }): HubLike
  /** A new hub ticket from the site's server; null when the person is signed out. */
  ticket(): Promise<string | null>
  setTimer?: (callback: () => void, ms: number) => unknown
  clearTimer?: (timer: unknown) => void
}

/** The client method the hub calls (IRealtimeClient.Receive); SignalR matches it in any case. */
export const receiveMethod = 'receive'

/** Delays between attempts: quickly at first, then every 30 seconds for as long as it takes. */
const delays = [0, 2_000, 5_000, 10_000]
export const maxRetryDelay = 30_000

export function retryDelay(previousRetryCount: number): number {
  return delays[previousRetryCount] ?? maxRetryDelay
}

export class SignedOutError extends Error {
  constructor() {
    super('Signed out: no realtime ticket')
    this.name = 'SignedOutError'
  }
}

export class RealtimeChannel {
  private readonly handlers = new Set<{ handler: RealtimeHandler; topic?: string }>()
  // How many listeners of the page asked for each topic (the likes and the comments of a post share its topic): the
  // hub hears of the first ask and of the last one taken back.
  private readonly topics = new Map<string, number>()
  private connection: HubLike | null = null
  private connected = false
  private running = false
  private signedOut = false
  private attempts = 0
  private timer: unknown = null

  constructor(private readonly options: ChannelOptions) {}

  /** Whether the channel is connected now (between a break and the reconnect it is not). */
  get isConnected(): boolean {
    return this.connected
  }

  /** Opens the connection and keeps it open until `stop` or sign-out. */
  start(): void {
    if (this.running) return
    this.running = true
    this.signedOut = false
    this.attempts = 0
    const connection = this.options.connect({
      accessTokenFactory: () => this.nextTicket(),
      retry: {
        nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
          this.running && !this.signedOut ? retryDelay(previousRetryCount) : null,
      },
    })
    connection.on(receiveMethod, (event) => this.dispatch(event))
    connection.onreconnected(() => {
      this.connected = true
      this.resubscribe()
    })
    connection.onclose(() => {
      this.connected = false
      // SignalR gave up (or never connected): start over unless the channel was stopped or the person signed out.
      if (this.running && !this.signedOut) this.schedule()
    })
    this.connection = connection
    void this.attempt()
  }

  async stop(): Promise<void> {
    this.running = false
    this.connected = false
    if (this.timer !== null) (this.options.clearTimer ?? clearTimeout)(this.timer as never)
    this.timer = null
    const connection = this.connection
    this.connection = null
    await connection?.stop()
  }

  /** Listens to every event, or to the events of one topic. Returns the function that stops listening. */
  on(handler: RealtimeHandler, topic?: string): () => void {
    const entry = { handler, topic }
    this.handlers.add(entry)
    return () => this.handlers.delete(entry)
  }

  /**
   * Receives the events of `topic` (e.g. `post:<id>`); kept across reconnects. The own account's is always on. Each
   * subscribe is taken back by its own unsubscribe: the topic stays while anyone on the page still asks for it.
   */
  async subscribe(topic: string): Promise<void> {
    const asks = (this.topics.get(topic) ?? 0) + 1
    this.topics.set(topic, asks)
    if (asks === 1 && this.connected) await this.send('Subscribe', topic)
  }

  async unsubscribe(topic: string): Promise<void> {
    const asks = (this.topics.get(topic) ?? 0) - 1
    if (asks > 0) {
      this.topics.set(topic, asks)
      return
    }
    this.topics.delete(topic)
    if (this.connected) await this.send('Unsubscribe', topic)
  }

  private async nextTicket(): Promise<string> {
    const ticket = await this.options.ticket()
    if (ticket === null) {
      this.signedOut = true
      throw new SignedOutError()
    }
    return ticket
  }

  private async attempt(): Promise<void> {
    const connection = this.connection
    if (!connection || !this.running) return
    try {
      await connection.start()
      if (connection !== this.connection) return
      this.connected = true
      this.attempts = 0
      this.resubscribe()
    } catch {
      // The first connect is not retried by SignalR itself; a break after it is (the retry policy above).
      if (this.running && !this.signedOut && connection === this.connection) this.schedule()
    }
  }

  private schedule(): void {
    if (this.timer !== null) return
    const delay = retryDelay(this.attempts++)
    this.timer = (this.options.setTimer ?? setTimeout)(() => {
      this.timer = null
      void this.attempt()
    }, delay)
  }

  private resubscribe(): void {
    for (const topic of this.topics.keys()) void this.send('Subscribe', topic)
  }

  private async send(method: string, topic: string): Promise<void> {
    try {
      await this.connection?.invoke(method, topic)
    } catch {
      // A refused topic or a break: the page simply gets no events of it; a reconnect subscribes again.
    }
  }

  private dispatch(event: RealtimeEvent): void {
    for (const { handler, topic } of this.handlers) {
      if (topic === undefined || topic === event.topic) handler(event)
    }
  }
}
