// The bookmarks of a signed-in person (E10-07, ADR-0113): with `postId`, those of a post and its comments for the post
// page (GET /posts/{postId}/bookmarks); with `ids`, those of the posts of a feed's cards (GET /bookmarks/posts). The
// server asks with the person's access token (ADR-0018); a guest has no bookmarks and gets none.
import {
  loadFeedBookmarks,
  loadPostBookmarks,
  maxFeedPosts,
  type FeedBookmarks,
  type PostBookmarks,
} from '~/api/bookmarks'

export default defineEventHandler(async (event): Promise<PostBookmarks | FeedBookmarks> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  if (!event.context.auth) throw createError({ statusCode: 401, message: 'Signed out' })
  const query = getQuery(event)
  if (typeof query.postId === 'string' && query.postId !== '') {
    const found = await loadPostBookmarks(ownApiClient(event), query.postId)
    if (!found) throw createError({ statusCode: 502, message: 'The bookmarks were not read' })
    return found
  }
  const ids = (Array.isArray(query.ids) ? query.ids : [query.ids]).filter(
    (id): id is string => typeof id === 'string' && id !== '',
  )
  if (ids.length === 0 || ids.length > maxFeedPosts)
    throw createError({ statusCode: 400, message: 'The posts are missing or too many' })
  const found = await loadFeedBookmarks(ownApiClient(event), ids)
  if (!found) throw createError({ statusCode: 502, message: 'The bookmarks were not read' })
  return found
})
