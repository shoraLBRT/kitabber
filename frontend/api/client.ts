// Typed API client (E01-08, ADR-0014). Types come only from api/schema.d.ts, which `pnpm run api:generate`
// builds from the OpenAPI snapshot backend/src/Api/openapi/v1.json; never write API request or response
// types by hand. tests/api-client.spec.ts fails when the file and the snapshot differ.
import createClient, { type ClientOptions } from 'openapi-fetch'
import type { paths } from './schema'

export type { paths, components } from './schema'

/** Paths in the schema already start with /api/v1, so baseUrl is the API origin ('' for the same origin). */
export function createApiClient(options: ClientOptions = {}) {
  return createClient<paths>(options)
}

export type ApiClient = ReturnType<typeof createApiClient>
