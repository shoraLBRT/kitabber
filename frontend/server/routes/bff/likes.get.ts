// The likes of a post and of its comments for a signed-in person (E10-01, ADR-0061): the server asks
// GET /posts/{postId}/likes with the person's access token (ADR-0018), so the answer says which of them the person
// likes; the page gets the counts and the marks, never the account. A guest reads the API directly.
import { loadLikes, type PostLikes } from '~/api/likes'

export default defineEventHandler(async (event): Promise<PostLikes> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const postId = getQuery(event).postId
  if (typeof postId !== 'string' || postId === '')
    throw createError({ statusCode: 400, message: 'The post is missing' })
  const likes = await loadLikes(ownApiClient(event), postId)
  if (!likes) throw createError({ statusCode: 502, message: 'The likes were not read' })
  return likes
})
