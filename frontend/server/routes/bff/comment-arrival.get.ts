// A comment that came in real time (E09-09, ADR-0112): the page heard `comments.posted` and asks here for the comment.
// The server reads it with the person's access token (ADR-0018), so the API marks the authors they blocked (ADR-0091),
// and reads which comments under the post are their own; the page gets the comment to show as new, if any, and the
// own ids — never the account. A guest gets nothing: the realtime channel is for the signed-in only (ADR-0019).
import { loadCommentArrival, type CommentArrival } from '~/api/comments'

export default defineEventHandler(async (event): Promise<CommentArrival> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const { postId, commentId } = getQuery(event)
  if (typeof postId !== 'string' || postId === '' || typeof commentId !== 'string' || commentId === '')
    throw createError({ statusCode: 400, message: 'The post or the comment is missing' })
  if (!event.context.auth) return { comment: null, own: [] }
  return await loadCommentArrival(ownApiClient(event), postId, commentId)
})
