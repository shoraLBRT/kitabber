// A page of the block «Блоги» or «Темы» of the search results (E14-03, ADR-0124) for the browser. Each row says
// whether the reader follows that blog and the reader's black list applies, so a signed-in person's request goes with
// their access token (it never leaves the server, ADR-0018) and the answer is never cached; a guest's goes without one.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { loadFoundBlogs, type BlogsLookup } from '~/api/search'

export default defineEventHandler(async (event): Promise<BlogsLookup> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const { q, kind, offset } = getQuery(event)
  if (typeof q !== 'string' || (kind !== 'personal' && kind !== 'topic'))
    throw createError({ statusCode: 400, message: 'The search is missing its query or kind' })
  const client = event.context.auth
    ? ownApiClient(event)
    : createApiClient({
        baseUrl: useRuntimeConfig().apiBase,
        headers: forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      })
  return loadFoundBlogs(client, q, kind, Number(offset) || 0)
})
