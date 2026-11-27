// The section «Закладки» (E04-12, ADR-0116): the tab asks here with `?tab=posts|comments&cursor=`, and the server asks
// the API with the person's access token (it never leaves the server, ADR-0018). Without one the section has nothing
// to show: 401. An unavailable API is a 502: the tab shows the error with «Повторить».
import { createApiClient } from '~/api/client'
import { loadBookmarkSection } from '~/api/bookmark-section'
import { forwardedHeaders } from '~/api/forwarded'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const query = getQuery(event)
  const tab = query.tab === 'comments' ? 'comments' : query.tab === 'posts' ? 'posts' : null
  if (!tab) throw createError({ statusCode: 400, message: 'The tab is posts or comments' })
  const accessToken = event.context.auth?.accessToken
  if (!accessToken) throw createError({ statusCode: 401, message: 'Sign in to see the bookmarks' })
  const cursor = typeof query.cursor === 'string' && query.cursor ? query.cursor : null
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      authorization: `Bearer ${accessToken}`,
    },
  })
  const found = await loadBookmarkSection(client, tab, cursor)
  if (found.status !== 'found')
    throw createError({ statusCode: 502, message: 'The bookmarks are unavailable' })
  return found.page
})
