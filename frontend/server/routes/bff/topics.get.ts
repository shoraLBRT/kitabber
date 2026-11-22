// The topics for the editor's topic choice (E05-09, ADR-0046), asked in the browser through the site's server like
// every other API read (ADR-0018). An unavailable API gives an empty list: the editor still offers «Без темы».
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { listTopics } from '~/api/topics'

export default defineEventHandler((event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
  })
  return listTopics(client)
})
