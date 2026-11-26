// The suggestions of the header's search (E14-02, ADR-0097) for the browser: the server asks
// GET /api/v1/search/suggestions with the person's access token when there is one (it never leaves the server,
// ADR-0018), so the reader's black list applies; a guest is asked for without one. The API's status and body are
// passed on; an unreachable API is a 502.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { searchSuggestions } from '~/api/search'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  const q = getQuery(event).q
  const result = await searchSuggestions(client, typeof q === 'string' ? q : '')
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
