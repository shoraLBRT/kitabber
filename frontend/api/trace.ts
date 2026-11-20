// W3C Trace Context for API calls (E01-09, ADR-0015). Every request to the API carries a `traceparent` header, so
// the API, its database queries and its logs share the trace id of the page that made the call. During server
// rendering all calls of one page join one trace: the one the proxy started, or a new one.

/** The trace a request belongs to: 32 hex digits of the trace id and the 2 hex digits of the flags. */
export interface TraceContext {
  traceId: string
  flags: string
}

const traceparentPattern = /^00-([0-9a-f]{32})-([0-9a-f]{16})-([0-9a-f]{2})$/

/** Reads a `traceparent` header; undefined when it is missing or malformed (all-zero ids are invalid). */
export function parseTraceparent(header: string | null | undefined): TraceContext | undefined {
  const match = header ? traceparentPattern.exec(header.trim().toLowerCase()) : null
  if (!match) return undefined
  const [, traceId, spanId, flags] = match as unknown as [string, string, string, string]
  if (/^0+$/.test(traceId) || /^0+$/.test(spanId)) return undefined
  return { traceId, flags }
}

/** A new sampled trace. */
export function newTraceContext(): TraceContext {
  return { traceId: randomHex(16), flags: '01' }
}

/** The trace of a server-rendered page: the incoming `traceparent` continues, otherwise a new trace starts. */
export function pageTrace(incoming: string | null | undefined): TraceContext {
  return parseTraceparent(incoming) ?? newTraceContext()
}

/** The `traceparent` of one API call in `trace`: the same trace id, a new span id. */
export function childTraceparent(trace: TraceContext): string {
  return `00-${trace.traceId}-${randomHex(8)}-${trace.flags}`
}

function randomHex(bytes: number): string {
  const buffer = new Uint8Array(bytes)
  do {
    globalThis.crypto.getRandomValues(buffer)
  } while (buffer.every((byte) => byte === 0))
  return Array.from(buffer, (byte) => byte.toString(16).padStart(2, '0')).join('')
}
