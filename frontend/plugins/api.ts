// The API client of the app as `useNuxtApp().$api` (ADR-0014). During server rendering every call of the page
// joins the trace of the incoming request, so one trace id leads from Nuxt through the API to the database
// (E01-09, ADR-0015); in the browser each call starts its own trace.
import { createApiClient } from '~/api/client'
import { pageTrace } from '~/api/trace'

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig()
  if (import.meta.server) {
    const trace = pageTrace(useRequestHeaders(['traceparent']).traceparent)
    return { provide: { api: createApiClient({ baseUrl: config.apiBase }, () => trace) } }
  }
  return { provide: { api: createApiClient({ baseUrl: config.public.apiBase }) } }
})
