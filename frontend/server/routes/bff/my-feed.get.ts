// A page of «Моя лента» (E08-04, ADR-0089) for the browser: the server asks GET /api/v1/feeds/my with the person's
// access token (it never leaves the server, ADR-0018) and passes the page on. The feed is one person's, so the answer
// is never cached. A guest has no feed.
import { loadMyFeed, type FeedLookup } from '~/api/feeds'

export default defineEventHandler(async (event): Promise<FeedLookup> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  if (!event.context.auth) throw createError({ statusCode: 401, message: 'Sign in to read your feed' })
  const cursor = getQuery(event).cursor
  return loadMyFeed(ownApiClient(event), typeof cursor === 'string' && cursor !== '' ? cursor : null)
})
