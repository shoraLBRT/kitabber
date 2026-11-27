// The widget «Популярные комментарии» (E08-12, ADR-0105): the right column asks here, and the server asks the API with
// the person's access token when there is one (it never leaves the server, ADR-0018), so the authors the person
// blocked are left out. An unavailable API is a 502: the widget shows the error with «Повторить».
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { loadPopularComments } from '~/api/popular-comments'

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
  const comments = await loadPopularComments(client)
  if (!comments) throw createError({ statusCode: 502, message: 'The popular comments are unavailable' })
  return comments
})
