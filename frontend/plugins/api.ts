// The API client of the app as `useNuxtApp().$api` (ADR-0014). During server rendering every call of the page
// joins the trace of the incoming request, so one trace id leads from Nuxt through the API to the database
// (E01-09, ADR-0015) and carries the reader's address for the API's rate limits (E01-10, ADR-0016); in the browser
// each call starts its own trace. On the server the calls also carry the signed-in person's access token (E18-02).
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { pageTrace } from '~/api/trace'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  if (import.meta.server) {
    const incoming = useRequestHeaders(['traceparent', 'x-forwarded-for'])
    const trace = pageTrace(incoming.traceparent)
    // The signed-in person's access token goes to the API from the server only (BFF, ADR-0018), so the API counts
    // the account's rate limits; the browser never sees it.
    const accessToken = useRequestEvent()?.context.auth?.accessToken
    const headers = {
      ...forwardedHeaders(incoming['x-forwarded-for']),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    }
    return { provide: { api: createApiClient({ baseUrl: config.apiBase, headers }, () => trace) } }
  }
  return { provide: { api: createApiClient({ baseUrl: config.public.apiBase }) } }
})
