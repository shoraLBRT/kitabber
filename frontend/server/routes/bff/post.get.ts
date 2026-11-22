// A post for the post page after the first render (E07-02): when the page moves to another post in the browser, it
// asks here, and the server asks the API with the person's access token (it never leaves the server, ADR-0018), so the
// author still sees their own draft. The answer is the page's outcome, with the post's canonical address.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { lookUpPost } from '~/api/posts'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const post = getQuery(event).post
  if (typeof post !== 'string') throw createError({ statusCode: 400, message: 'The post is missing' })
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  return lookUpPost(client, post)
})
