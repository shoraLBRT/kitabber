// The reactions of a post and of its comments for a signed-in person (E10-01, E10-02, ADR-0061): the server asks
// GET /posts/{postId}/reactions with the person's access token (ADR-0018), so the answer says which reactions are the
// person's; the page gets the counts and the own reactions, never the account. A guest reads the API directly.
import { loadReactions, type PostReactions } from '~/api/reactions'

export default defineEventHandler(async (event): Promise<PostReactions> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const postId = getQuery(event).postId
  if (typeof postId !== 'string' || postId === '')
    throw createError({ statusCode: 400, message: 'The post is missing' })
  const reactions = await loadReactions(ownApiClient(event), postId)
  if (!reactions) throw createError({ statusCode: 502, message: 'The reactions were not read' })
  return reactions
})
