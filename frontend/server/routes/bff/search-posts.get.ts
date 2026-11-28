// A page of the block «Посты» of the search results (E14-03, ADR-0124) for the browser, with its filters: the titles
// only, the period, the order. The reader's black list applies, so a signed-in person's request goes with their access
// token (it never leaves the server, ADR-0018) and the answer is never cached; a guest's goes without one.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { filtersOf, loadFoundPosts, type PostsLookup } from '~/api/search'

export default defineEventHandler(async (event): Promise<PostsLookup> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const query = getQuery(event)
  if (typeof query.q !== 'string')
    throw createError({ statusCode: 400, message: 'The search is missing its query' })
  const client = event.context.auth
    ? ownApiClient(event)
    : createApiClient({
        baseUrl: useRuntimeConfig().apiBase,
        headers: forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      })
  // The filters as the address keeps them; anything unknown is the default.
  const filters = filtersOf({
    titles: query.titlesOnly === 'true' ? '1' : undefined,
    period: query.period,
    order: query.order,
  })
  return loadFoundPosts(client, query.q, filters, Number(query.offset) || 0)
})
