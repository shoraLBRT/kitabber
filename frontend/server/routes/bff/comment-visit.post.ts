// The opening of a post's comments by a signed-in person (E09-08, ADR-0109): the server asks
// POST /posts/{postId}/comments/visit with the person's access token (ADR-0018) and gives the page the comments new since
// the previous opening. A guest, or an API that fails, has none new: nothing is marked.
import { noVisit, visitComments, type CommentVisit } from '~/api/comments'

export default defineEventHandler(async (event): Promise<CommentVisit> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const body = await readBody<{ postId?: unknown } | null>(event).catch(() => null)
  const postId = body?.postId
  if (typeof postId !== 'string' || postId === '')
    throw createError({ statusCode: 400, message: 'The post is missing' })
  if (!event.context.auth) return noVisit
  return await visitComments(ownApiClient(event), postId)
})
