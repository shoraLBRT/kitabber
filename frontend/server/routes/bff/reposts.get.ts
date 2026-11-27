// The list of a post's reposts (E10-08, ADR-0117): the post page asks here with `?postId=<id>&cursor=`, and the server
// asks the API with the person's access token when there is one (it never leaves the server, ADR-0018), so the people
// the person blocked are left out. An unavailable API is a 502: the list shows the error with «Повторить».
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { loadReposts } from '~/api/reposts'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const query = getQuery(event)
  const postId = typeof query.postId === 'string' ? query.postId : ''
  if (!/^[0-9a-fA-F-]{36}$/.test(postId))
    throw createError({ statusCode: 400, message: 'A post id is required' })
  const cursor = typeof query.cursor === 'string' && query.cursor ? query.cursor : null
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  const found = await loadReposts(client, postId, cursor)
  if (found.status !== 'found') throw createError({ statusCode: 502, message: 'The reposts are unavailable' })
  return found.page
})
