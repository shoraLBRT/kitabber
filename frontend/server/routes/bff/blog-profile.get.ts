// A blog's profile for the blog page after the first render (E04-02): when the page moves to another blog in the
// browser, it asks here, and the server asks the API with the person's access token (it never leaves the server,
// ADR-0018), so the owner still sees their own blog as the owner. The answer is the page's outcome, not the API's.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { lookUpProfile } from '~/api/profiles'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const blog = getQuery(event).blog
  if (typeof blog !== 'string') throw createError({ statusCode: 400, message: 'The blog is missing' })
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  return lookUpProfile(client, blog)
})
