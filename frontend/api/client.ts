// Typed API client (E01-08, ADR-0014). Types come only from api/schema.d.ts, which `pnpm run api:generate`
// builds from the OpenAPI snapshot backend/src/Api/openapi/v1.json; never write API request or response
// types by hand. tests/api-client.spec.ts fails when the file and the snapshot differ.
import createClient, { type ClientOptions } from 'openapi-fetch'
import type { paths } from './schema'
import { childTraceparent, newTraceContext, type TraceContext } from './trace'

export type { paths, components } from './schema'

/**
 * Paths in the schema already start with /api/v1, so baseUrl is the API origin ('' for the same origin).
 * Every request carries a W3C `traceparent` (E01-09, ADR-0015) in the trace that `trace` returns; without it each
 * request starts its own trace. plugins/api.ts gives server rendering one trace per page.
 */
export function createApiClient(options: ClientOptions = {}, trace: () => TraceContext = newTraceContext) {
  const client = createClient<paths>(options)
  client.use({
    onRequest({ request }) {
      request.headers.set('traceparent', childTraceparent(trace()))
      return request
    },
  })
  return client
}

export type ApiClient = ReturnType<typeof createApiClient>
