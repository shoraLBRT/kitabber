// The management of a topic for its page after the first render (E11-12, ADR-0121): the server asks
// GET /api/v1/topics/{id}/management with the person's access token when there is one (ADR-0018), so the role is the
// reader's. The answer is the page's outcome, not the API's.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { loadTopicManagement } from '~/api/topic-management'

export default defineEventHandler((event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const id = getQuery(event).id
  if (typeof id !== 'string') throw createError({ statusCode: 400, message: 'The topic is missing' })
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  return loadTopicManagement(client, id)
})
