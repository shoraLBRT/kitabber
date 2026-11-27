// The tab «Комментарии» of a blog and a topic (E09-15, ADR-0114): the tab asks here with `?blog=<id>&cursor=`, and the
// server asks the API with the person's access token when there is one (it never leaves the server, ADR-0018), so the
// authors the person blocked are left out after a reload too. An unavailable API is a 502: the tab shows the error
// with «Повторить».
import { createApiClient } from '~/api/client'
import { loadBlogComments } from '~/api/blog-comments'
import { forwardedHeaders } from '~/api/forwarded'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const query = getQuery(event)
  const blog = typeof query.blog === 'string' ? query.blog : ''
  if (!/^[0-9a-fA-F-]{36}$/.test(blog))
    throw createError({ statusCode: 400, message: 'A blog id is required' })
  const cursor = typeof query.cursor === 'string' && query.cursor ? query.cursor : null
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  const found = await loadBlogComments(client, blog, cursor)
  if (found.status !== 'found')
    throw createError({ statusCode: 502, message: 'The blog comments are unavailable' })
  return found.page
})
